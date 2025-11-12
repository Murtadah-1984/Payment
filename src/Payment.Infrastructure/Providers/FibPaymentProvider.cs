using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class FibPaymentProvider : IPaymentProvider, IPaymentCallbackProvider
{
    private readonly ILogger<FibPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly FibProviderConfiguration _config;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string ProviderName => "FIB";

    public FibPaymentProvider(
        ILogger<FibPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(FibPaymentProvider));
        _config = new FibProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:FIB:BaseUrl"] ?? "https://fib.stage.fib.iq",
            ClientId = configuration["PaymentProviders:FIB:ClientId"] ?? string.Empty,
            ClientSecret = configuration["PaymentProviders:FIB:ClientSecret"] ?? string.Empty,
            StatusCallbackUrl = configuration["PaymentProviders:FIB:StatusCallbackUrl"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:FIB:IsTestMode", true)
        };
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with FIB for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.ClientId) || string.IsNullOrEmpty(_config.ClientSecret))
            {
                throw new InvalidOperationException("FIB ClientId and ClientSecret must be configured");
            }

            // FIB only supports IQD currency
            if (request.Currency.Code != "IQD")
            {
                _logger.LogWarning("FIB only supports IQD currency. Converting {Currency} to IQD", request.Currency.Code);
                // TODO: Implement currency conversion if needed
            }

            // Ensure we have a valid access token
            await EnsureValidAccessTokenAsync(cancellationToken);

            // Create payment request
            var createPaymentRequest = new FibCreatePaymentRequest
            {
                MonetaryValue = new FibMonetaryValue
                {
                    Amount = request.Amount.Value.ToString("F2"),
                    Currency = "IQD"
                },
                StatusCallbackUrl = _config.StatusCallbackUrl,
                Description = request.Metadata?.GetValueOrDefault("description") ?? 
                             $"Payment for order {request.OrderId}"
            };

            // Truncate description to 50 characters max
            if (createPaymentRequest.Description != null && createPaymentRequest.Description.Length > 50)
            {
                createPaymentRequest.Description = createPaymentRequest.Description.Substring(0, 50);
            }

            var jsonContent = JsonSerializer.Serialize(createPaymentRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var apiUrl = $"{_config.BaseUrl}/protected/v1/payments";
            _logger.LogInformation("Sending payment creation request to FIB for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                var paymentResponse = JsonSerializer.Deserialize<FibCreatePaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse != null && !string.IsNullOrEmpty(paymentResponse.PaymentId))
                {
                    _logger.LogInformation("FIB payment created successfully. Payment ID: {PaymentId}", paymentResponse.PaymentId);

                    // Handle split payment if required
                    if (request.SplitPayment != null)
                    {
                        _logger.LogInformation("Split payment required: System={SystemShare}, Owner={OwnerShare}",
                            request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);
                        
                        // Note: FIB doesn't natively support split payments
                        // You would need to process two separate transactions or handle it at the application level
                    }

                    return new PaymentResult(
                        Success: true,
                        TransactionId: paymentResponse.PaymentId,
                        FailureReason: null,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "FIB" },
                            { "PaymentId", paymentResponse.PaymentId },
                            { "QrCode", paymentResponse.QrCode ?? string.Empty },
                            { "ReadableCode", paymentResponse.ReadableCode ?? string.Empty },
                            { "BusinessAppLink", paymentResponse.BusinessAppLink ?? string.Empty },
                            { "CorporateAppLink", paymentResponse.CorporateAppLink ?? string.Empty },
                            { "ValidUntil", paymentResponse.ValidUntil ?? string.Empty },
                            { "ProcessedAt", DateTime.UtcNow.ToString("O") },
                            { "IsTestMode", _config.IsTestMode.ToString() }
                        });
                }
                else
                {
                    _logger.LogWarning("FIB payment response was invalid or missing PaymentId. Response: {Response}", responseString);
                    return new PaymentResult(
                        Success: false,
                        TransactionId: null,
                        FailureReason: "Invalid payment response from FIB",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "StatusCode", response.StatusCode.ToString() },
                            { "Response", responseString }
                        });
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotAcceptable)
            {
                _logger.LogWarning("FIB payment creation not accepted. Response: {Response}", responseString);
                return new PaymentResult(
                    Success: false,
                    TransactionId: null,
                    FailureReason: "Payment request not accepted by FIB",
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "StatusCode", response.StatusCode.ToString() },
                        { "Response", responseString }
                    });
            }
            else
            {
                _logger.LogError("FIB API returned error status {StatusCode}: {Response}",
                    response.StatusCode, responseString);
                return new PaymentResult(
                    Success: false,
                    TransactionId: null,
                    FailureReason: $"FIB API error: {response.StatusCode}",
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "StatusCode", response.StatusCode.ToString() },
                        { "Response", responseString }
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing FIB payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"FIB payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Verifies a payment callback from FIB (implements IPaymentCallbackProvider)
    /// </summary>
    public async Task<PaymentResult> VerifyCallbackAsync(
        Dictionary<string, string> callbackData,
        CancellationToken cancellationToken = default)
    {
        if (!callbackData.TryGetValue("paymentId", out var paymentId) && 
            !callbackData.TryGetValue("PaymentId", out paymentId))
        {
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: "Payment ID is required in callback data",
                ProviderMetadata: null);
        }

        return await CheckPaymentStatusAsync(paymentId!, cancellationToken);
    }

    /// <summary>
    /// Checks the status of a FIB payment
    /// </summary>
    public async Task<PaymentResult> CheckPaymentStatusAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureValidAccessTokenAsync(cancellationToken);

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var apiUrl = $"{_config.BaseUrl}/protected/v1/payments/{paymentId}/status";
            _logger.LogInformation("Checking FIB payment status for Payment ID: {PaymentId}", paymentId);

            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var statusResponse = JsonSerializer.Deserialize<FibStatusResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (statusResponse != null)
                {
                    var isSuccess = statusResponse.Status?.ToUpper() == "PAID";
                    var failureReason = statusResponse.Status?.ToUpper() == "DECLINED" 
                        ? $"Payment declined: {statusResponse.DecliningReason}" 
                        : null;

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: statusResponse.PaymentId,
                        FailureReason: failureReason,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "FIB" },
                            { "PaymentId", statusResponse.PaymentId ?? string.Empty },
                            { "Status", statusResponse.Status ?? string.Empty },
                            { "ValidUntil", statusResponse.ValidUntil ?? string.Empty },
                            { "PaidAt", statusResponse.PaidAt ?? string.Empty },
                            { "DeclinedAt", statusResponse.DeclinedAt ?? string.Empty },
                            { "DecliningReason", statusResponse.DecliningReason ?? string.Empty },
                            { "Amount", statusResponse.Amount?.Amount?.ToString() ?? string.Empty },
                            { "Currency", statusResponse.Amount?.Currency ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: paymentId,
                FailureReason: $"Failed to check payment status: {response.StatusCode}",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking FIB payment status for {PaymentId}", paymentId);
            return new PaymentResult(
                Success: false,
                TransactionId: paymentId,
                FailureReason: $"Status check failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Cancels a FIB payment
    /// </summary>
    public async Task<bool> CancelPaymentAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureValidAccessTokenAsync(cancellationToken);

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var apiUrl = $"{_config.BaseUrl}/protected/v1/payments/{paymentId}/cancel";
            _logger.LogInformation("Cancelling FIB payment for Payment ID: {PaymentId}", paymentId);

            var response = await _httpClient.PostAsync(apiUrl, null, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                _logger.LogInformation("FIB payment cancelled successfully. Payment ID: {PaymentId}", paymentId);
                return true;
            }

            _logger.LogWarning("Failed to cancel FIB payment. Status: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling FIB payment for {PaymentId}", paymentId);
            return false;
        }
    }

    /// <summary>
    /// Ensures we have a valid OAuth2 access token
    /// </summary>
    private async Task EnsureValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        // Check if token is still valid (with 5 minute buffer)
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
        {
            return;
        }

        _logger.LogInformation("Acquiring new FIB access token");

        var tokenUrl = $"{_config.BaseUrl}/oauth/token";
        
        var tokenRequest = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", _config.ClientId),
            new("client_secret", _config.ClientSecret)
        };

        var tokenContent = new FormUrlEncodedContent(tokenRequest);
        
        // Remove authorization header for token request
        _httpClient.DefaultRequestHeaders.Authorization = null;

        var response = await _httpClient.PostAsync(tokenUrl, tokenContent, cancellationToken);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = JsonSerializer.Deserialize<FibTokenResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _accessToken = tokenResponse.AccessToken;
                // Default token expiry to 1 hour if not provided
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn > 0 
                    ? tokenResponse.ExpiresIn 
                    : 3600);
                
                _logger.LogInformation("FIB access token acquired successfully. Expires at: {Expiry}", _tokenExpiry);
            }
            else
            {
                throw new InvalidOperationException("Failed to parse FIB token response");
            }
        }
        else
        {
            throw new InvalidOperationException($"Failed to acquire FIB access token: {response.StatusCode} - {responseString}");
        }
    }

    // FIB API Models
    private class FibCreatePaymentRequest
    {
        public FibMonetaryValue MonetaryValue { get; set; } = null!;
        public string? StatusCallbackUrl { get; set; }
        public string? Description { get; set; }
    }

    private class FibMonetaryValue
    {
        public string Amount { get; set; } = string.Empty;
        public string Currency { get; set; } = "IQD";
    }

    private class FibCreatePaymentResponse
    {
        public string? PaymentId { get; set; }
        public string? QrCode { get; set; }
        public string? ReadableCode { get; set; }
        public string? BusinessAppLink { get; set; }
        public string? CorporateAppLink { get; set; }
        public string? ValidUntil { get; set; }
    }

    private class FibStatusResponse
    {
        public string? PaymentId { get; set; }
        public string? Status { get; set; }
        public string? ValidUntil { get; set; }
        public string? PaidAt { get; set; }
        public FibMonetaryValue? Amount { get; set; }
        public string? DecliningReason { get; set; }
        public string? DeclinedAt { get; set; }
        public FibPaidBy? PaidBy { get; set; }
    }

    private class FibPaidBy
    {
        public string? Name { get; set; }
        public string? Iban { get; set; }
    }

    private class FibTokenResponse
    {
        public string? AccessToken { get; set; }
        public string? TokenType { get; set; }
        public int ExpiresIn { get; set; }
    }
}

