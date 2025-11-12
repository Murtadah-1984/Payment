using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class CheckoutPaymentProvider : IPaymentProvider
{
    private readonly ILogger<CheckoutPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly CheckoutProviderConfiguration _config;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string ProviderName => "Checkout";

    public CheckoutPaymentProvider(
        ILogger<CheckoutPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(CheckoutPaymentProvider));
        _config = new CheckoutProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Checkout:BaseUrl"] ?? "https://api.checkout.com/",
            SecretKey = configuration["PaymentProviders:Checkout:SecretKey"] ?? string.Empty,
            PublicKey = configuration["PaymentProviders:Checkout:PublicKey"] ?? string.Empty,
            ClientId = configuration["PaymentProviders:Checkout:ClientId"] ?? string.Empty,
            ClientSecret = configuration["PaymentProviders:Checkout:ClientSecret"] ?? string.Empty,
            UseOAuth = configuration.GetValue<bool>("PaymentProviders:Checkout:UseOAuth", false),
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Checkout:IsTestMode", true)
        };

        // Set up authentication
        if (!_config.UseOAuth && !string.IsNullOrEmpty(_config.SecretKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.SecretKey);
        }

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Checkout.com for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (_config.UseOAuth)
            {
                if (string.IsNullOrEmpty(_config.ClientId) || string.IsNullOrEmpty(_config.ClientSecret))
                {
                    throw new InvalidOperationException("Checkout.com ClientId and ClientSecret must be configured for OAuth");
                }
                await EnsureValidAccessTokenAsync(cancellationToken);
            }
            else if (string.IsNullOrEmpty(_config.SecretKey))
            {
                throw new InvalidOperationException("Checkout.com SecretKey must be configured");
            }

            // Convert amount to smallest currency unit (Checkout.com uses minor currency unit)
            var amountInSmallestUnit = ConvertToSmallestCurrencyUnit(request.Amount.Value, request.Currency.Code);

            // Checkout.com requires source (payment method token) from frontend
            // This should be provided in metadata
            if (request.Metadata == null || !request.Metadata.TryGetValue("source", out var source) || string.IsNullOrEmpty(source))
            {
                throw new InvalidOperationException("Checkout.com requires 'source' (payment token) in metadata. This should be obtained from Checkout.com's JavaScript SDK.");
            }

            // Create payment request
            var paymentRequest = new CheckoutPaymentRequest
            {
                Amount = amountInSmallestUnit,
                Currency = request.Currency.Code,
                Reference = request.OrderId,
                Description = request.Metadata?.GetValueOrDefault("description") ?? $"Payment for order {request.OrderId}",
                Source = new CheckoutSource
                {
                    Type = "token",
                    Token = source
                },
                Customer = new CheckoutCustomer
                {
                    Email = request.Metadata?.GetValueOrDefault("customer_email") ?? string.Empty,
                    Name = request.Metadata?.GetValueOrDefault("customer_name") ?? string.Empty
                },
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", request.OrderId },
                    { "merchant_id", request.MerchantId }
                }
            };

            // Add 3DS information if available
            if (request.Metadata?.TryGetValue("3ds_enabled", out var threeDsEnabled) == true && 
                bool.TryParse(threeDsEnabled, out var enable3ds) && enable3ds)
            {
                paymentRequest.ThreeDs = new CheckoutThreeDs
                {
                    Enabled = true
                };
            }

            // Add customer IP address if available
            if (request.Metadata?.TryGetValue("customer_ip", out var customerIp) == true)
            {
                paymentRequest.IpAddress = customerIp;
            }

            // Handle split payments
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                // Checkout.com supports split payments via transfers
                // You would need to set up transfers to connected accounts
                if (request.Metadata?.TryGetValue("connected_account_id", out var connectedAccountId) == true)
                {
                    var ownerShareSmallestUnit = ConvertToSmallestCurrencyUnit(request.SplitPayment.OwnerShare, request.Currency.Code);
                    
                    paymentRequest.Transfers = new List<CheckoutTransfer>
                    {
                        new CheckoutTransfer
                        {
                            Destination = connectedAccountId,
                            Amount = ownerShareSmallestUnit
                        }
                    };

                    _logger.LogInformation("Checkout.com split payment configured: Owner={OwnerShareSmallestUnit} smallest units to connected account",
                        ownerShareSmallestUnit);
                }
                else
                {
                    _logger.LogWarning("Split payment requested but connected_account_id not provided. Processing as single payment.");
                }
            }

            var jsonContent = JsonSerializer.Serialize(paymentRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var apiUrl = $"{_config.BaseUrl}payments";
            _logger.LogInformation("Creating Checkout.com payment for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Created || 
                response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                var paymentResponse = JsonSerializer.Deserialize<CheckoutPaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse != null)
                {
                    var isSuccess = paymentResponse.Status == "Authorized" || paymentResponse.Status == "Captured";
                    var requiresAction = paymentResponse.Status == "Pending" || paymentResponse.Status == "RequiresAction";

                    _logger.LogInformation("Checkout.com payment processed. Payment ID: {PaymentId}, Status: {Status}",
                        paymentResponse.Id, paymentResponse.Status);

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentResponse.Id,
                        FailureReason: isSuccess ? null : $"Payment status: {paymentResponse.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "Checkout" },
                            { "PaymentId", paymentResponse.Id ?? string.Empty },
                            { "Status", paymentResponse.Status ?? string.Empty },
                            { "ActionId", paymentResponse.ActionId ?? string.Empty },
                            { "RequiresAction", requiresAction.ToString() },
                            { "Amount", paymentResponse.Amount.ToString() },
                            { "Currency", paymentResponse.Currency ?? string.Empty },
                            { "Reference", paymentResponse.Reference ?? string.Empty },
                            { "ResponseCode", paymentResponse.ResponseCode ?? string.Empty },
                            { "ResponseSummary", paymentResponse.ResponseSummary ?? string.Empty },
                            { "ProcessedAt", DateTime.UtcNow.ToString("O") },
                            { "IsTestMode", _config.IsTestMode.ToString() }
                        });
                }
            }

            // Handle errors
            var errorResponse = JsonSerializer.Deserialize<CheckoutErrorResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var errorMessage = errorResponse?.ErrorType ?? $"Checkout.com API error: {response.StatusCode}";
            if (errorResponse?.ErrorCodes != null && errorResponse.ErrorCodes.Count > 0)
            {
                errorMessage = $"{errorMessage} - {string.Join(", ", errorResponse.ErrorCodes)}";
            }

            _logger.LogError("Checkout.com payment creation failed: {ErrorMessage}", errorMessage);

            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: errorMessage,
                ProviderMetadata: new Dictionary<string, string>
                {
                    { "StatusCode", response.StatusCode.ToString() },
                    { "Response", responseString }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Checkout.com payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Checkout.com payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Captures a payment
    /// </summary>
    public async Task<PaymentResult> CapturePaymentAsync(string paymentId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var captureRequest = new CheckoutCaptureRequest
            {
                Amount = amount.HasValue ? ConvertToSmallestCurrencyUnit(amount.Value, "USD") : null
            };

            var jsonContent = JsonSerializer.Serialize(captureRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"{_config.BaseUrl}payments/{paymentId}/captures";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentResponse = JsonSerializer.Deserialize<CheckoutPaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse != null)
                {
                    var isSuccess = paymentResponse.Status == "Captured";
                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentResponse.Id,
                        FailureReason: isSuccess ? null : $"Payment status: {paymentResponse.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Status", paymentResponse.Status ?? string.Empty },
                            { "ActionId", paymentResponse.ActionId ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: paymentId,
                FailureReason: "Capture failed",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing Checkout.com payment {PaymentId}", paymentId);
            return new PaymentResult(
                Success: false,
                TransactionId: paymentId,
                FailureReason: $"Capture failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Refunds a payment
    /// </summary>
    public async Task<PaymentResult> RefundPaymentAsync(string paymentId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var refundRequest = new CheckoutRefundRequest
            {
                Amount = amount.HasValue ? ConvertToSmallestCurrencyUnit(amount.Value, "USD") : null,
                Reference = $"REF-{Guid.NewGuid()}"
            };

            var jsonContent = JsonSerializer.Serialize(refundRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"{_config.BaseUrl}payments/{paymentId}/refunds";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var refundResponse = JsonSerializer.Deserialize<CheckoutRefundResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (refundResponse != null)
                {
                    return new PaymentResult(
                        Success: true,
                        TransactionId: refundResponse.ActionId ?? paymentId,
                        FailureReason: null,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "RefundId", refundResponse.ActionId ?? string.Empty },
                            { "Status", refundResponse.Status ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: paymentId,
                FailureReason: "Refund failed",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding Checkout.com payment {PaymentId}", paymentId);
            return new PaymentResult(
                Success: false,
                TransactionId: paymentId,
                FailureReason: $"Refund failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Ensures we have a valid OAuth2 access token
    /// </summary>
    private async Task EnsureValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
        {
            return;
        }

        _logger.LogInformation("Acquiring new Checkout.com access token");

        var tokenUrl = $"{_config.BaseUrl}access-tokens";
        var tokenRequest = new
        {
            client_id = _config.ClientId,
            client_secret = _config.ClientSecret,
            grant_type = "client_credentials"
        };

        var jsonContent = JsonSerializer.Serialize(tokenRequest);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = null;

        var response = await _httpClient.PostAsync(tokenUrl, content, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = JsonSerializer.Deserialize<CheckoutTokenResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _accessToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600);
                
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                
                _logger.LogInformation("Checkout.com access token acquired successfully");
            }
        }
    }

    private long ConvertToSmallestCurrencyUnit(decimal amount, string currency)
    {
        var currenciesWithoutSubunits = new[] { "JPY", "KRW", "VND" };
        if (currenciesWithoutSubunits.Contains(currency, StringComparer.OrdinalIgnoreCase))
        {
            return (long)amount;
        }
        return (long)(amount * 100);
    }

    // Checkout.com API Models
    private class CheckoutPaymentRequest
    {
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string? Description { get; set; }
        public CheckoutSource Source { get; set; } = null!;
        public CheckoutCustomer? Customer { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public CheckoutThreeDs? ThreeDs { get; set; }
        public string? IpAddress { get; set; }
        public List<CheckoutTransfer>? Transfers { get; set; }
    }

    private class CheckoutSource
    {
        public string Type { get; set; } = "token";
        public string Token { get; set; } = string.Empty;
    }

    private class CheckoutCustomer
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private class CheckoutThreeDs
    {
        public bool Enabled { get; set; }
    }

    private class CheckoutTransfer
    {
        public string Destination { get; set; } = string.Empty;
        public long Amount { get; set; }
    }

    private class CheckoutCaptureRequest
    {
        public long? Amount { get; set; }
    }

    private class CheckoutRefundRequest
    {
        public long? Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
    }

    private class CheckoutPaymentResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public string? ActionId { get; set; }
        public long Amount { get; set; }
        public string? Currency { get; set; }
        public string? Reference { get; set; }
        public string? ResponseCode { get; set; }
        public string? ResponseSummary { get; set; }
    }

    private class CheckoutRefundResponse
    {
        public string? ActionId { get; set; }
        public string? Status { get; set; }
    }

    private class CheckoutTokenResponse
    {
        public string? AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }

    private class CheckoutErrorResponse
    {
        public string? ErrorType { get; set; }
        public List<string>? ErrorCodes { get; set; }
    }
}

