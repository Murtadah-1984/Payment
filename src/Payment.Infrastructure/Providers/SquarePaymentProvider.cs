using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class SquarePaymentProvider : IPaymentProvider
{
    private readonly ILogger<SquarePaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly SquareProviderConfiguration _config;

    public string ProviderName => "Square";

    public SquarePaymentProvider(
        ILogger<SquarePaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(SquarePaymentProvider));
        _config = new SquareProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Square:BaseUrl"] ?? "https://connect.squareup.com/v2/",
            AccessToken = configuration["PaymentProviders:Square:AccessToken"] ?? string.Empty,
            ApplicationId = configuration["PaymentProviders:Square:ApplicationId"] ?? string.Empty,
            LocationId = configuration["PaymentProviders:Square:LocationId"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Square:IsTestMode", true)
        };

        // Set up authentication header
        // Square uses Bearer token with access token
        if (!string.IsNullOrEmpty(_config.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.AccessToken);
        }

        // Set Square API version
        _httpClient.DefaultRequestHeaders.Add("Square-Version", "2024-01-18");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Square for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.AccessToken))
            {
                throw new InvalidOperationException("Square AccessToken must be configured");
            }

            if (string.IsNullOrEmpty(_config.LocationId))
            {
                throw new InvalidOperationException("Square LocationId must be configured");
            }

            // Square requires source_id (payment token/nonce) from frontend
            // This should be provided in metadata
            if (request.Metadata == null || !request.Metadata.TryGetValue("source_id", out var sourceId) || string.IsNullOrEmpty(sourceId))
            {
                throw new InvalidOperationException("Square requires source_id in metadata. This should be obtained from Square Web Payments SDK or In-App Payments SDK.");
            }

            // Convert amount to smallest currency unit (Square uses amount in smallest currency unit)
            var amountInSmallestUnit = ConvertToSmallestCurrencyUnit(request.Amount.Value, request.Currency.Code);

            // Generate idempotency key (Square requires this for idempotent requests)
            var idempotencyKey = Guid.NewGuid().ToString();

            // Create payment request
            var createPaymentRequest = new SquareCreatePaymentRequest
            {
                SourceId = sourceId,
                IdempotencyKey = idempotencyKey,
                AmountMoney = new SquareMoney
                {
                    Amount = amountInSmallestUnit,
                    Currency = request.Currency.Code
                },
                LocationId = _config.LocationId
            };

            // Add reference ID (order ID)
            createPaymentRequest.ReferenceId = request.OrderId;

            // Add note/description if available
            if (request.Metadata.TryGetValue("description", out var description))
            {
                createPaymentRequest.Note = description;
            }

            // Handle split payments using Square's application fees
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                var systemShareSmallestUnit = ConvertToSmallestCurrencyUnit(request.SplitPayment.SystemShare, request.Currency.Code);
                
                // Square supports application fees for split payments
                createPaymentRequest.ApplicationDetails = new SquareApplicationDetails
                {
                    SquareProduct = "ECOMMERCE_API"
                };

                // For split payments, you typically use Square Connect with connected accounts
                // Application fee is the platform's share
                if (request.Metadata.TryGetValue("connected_account_id", out var connectedAccountId))
                {
                    // If using Square Connect, transfer to connected account
                    createPaymentRequest.TransferData = new SquareTransferData
                    {
                        Destination = connectedAccountId
                    };
                }

                _logger.LogInformation("Square split payment configured: System={SystemShareSmallestUnit} smallest units",
                    systemShareSmallestUnit);
            }

            var jsonContent = JsonSerializer.Serialize(createPaymentRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var apiUrl = $"{_config.BaseUrl}payments";
            _logger.LogInformation("Creating Square payment for order {OrderId} with idempotency key {IdempotencyKey}",
                request.OrderId, idempotencyKey);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentResponse = JsonSerializer.Deserialize<SquarePaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse?.Payment != null)
                {
                    var payment = paymentResponse.Payment;
                    var isSuccess = payment.Status?.ToUpper() == "COMPLETED";
                    var failureReason = !isSuccess ? $"Payment status: {payment.Status}" : null;

                    _logger.LogInformation("Square payment created successfully. Payment ID: {PaymentId}, Status: {Status}",
                        payment.Id, payment.Status);

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: payment.Id,
                        FailureReason: failureReason,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "Square" },
                            { "PaymentId", payment.Id ?? string.Empty },
                            { "Status", payment.Status ?? string.Empty },
                            { "ReceiptNumber", payment.ReceiptNumber ?? string.Empty },
                            { "ReceiptUrl", payment.ReceiptUrl ?? string.Empty },
                            { "OrderId", payment.OrderId ?? string.Empty },
                            { "Amount", payment.AmountMoney?.Amount.ToString() ?? string.Empty },
                            { "Currency", payment.AmountMoney?.Currency ?? string.Empty },
                            { "CreatedAt", payment.CreatedAt ?? string.Empty },
                            { "UpdatedAt", payment.UpdatedAt ?? string.Empty },
                            { "IdempotencyKey", idempotencyKey },
                            { "IsTestMode", _config.IsTestMode.ToString() }
                        });
                }
            }

            // Handle Square errors
            var errorResponse = JsonSerializer.Deserialize<SquareErrorResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var errorMessage = "Unknown error from Square";
            if (errorResponse?.Errors != null && errorResponse.Errors.Count > 0)
            {
                var firstError = errorResponse.Errors[0];
                errorMessage = $"{firstError.Category}: {firstError.Code} - {firstError.Detail}";
            }
            else
            {
                errorMessage = $"Square API error: {response.StatusCode}";
            }

            _logger.LogError("Square payment creation failed: {ErrorMessage}", errorMessage);

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
            _logger.LogError(ex, "Error processing Square payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Square payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Retrieves a Square payment by ID
    /// </summary>
    public async Task<PaymentResult> GetPaymentAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiUrl = $"{_config.BaseUrl}payments/{paymentId}";
            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentResponse = JsonSerializer.Deserialize<SquarePaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse?.Payment != null)
                {
                    var payment = paymentResponse.Payment;
                    var isSuccess = payment.Status?.ToUpper() == "COMPLETED";

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: payment.Id,
                        FailureReason: isSuccess ? null : $"Payment status: {payment.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Status", payment.Status ?? string.Empty },
                            { "Amount", payment.AmountMoney?.Amount.ToString() ?? string.Empty },
                            { "Currency", payment.AmountMoney?.Currency ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: paymentId,
                FailureReason: "Failed to retrieve payment",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Square payment {PaymentId}", paymentId);
            return new PaymentResult(
                Success: false,
                TransactionId: paymentId,
                FailureReason: $"Retrieval failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Cancels a Square payment
    /// </summary>
    public async Task<bool> CancelPaymentAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cancelRequest = new SquareCancelPaymentRequest
            {
                IdempotencyKey = Guid.NewGuid().ToString()
            };

            var jsonContent = JsonSerializer.Serialize(cancelRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"{_config.BaseUrl}payments/{paymentId}/cancel";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Square payment cancelled successfully. Payment ID: {PaymentId}", paymentId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Square payment {PaymentId}", paymentId);
            return false;
        }
    }

    private long ConvertToSmallestCurrencyUnit(decimal amount, string currency)
    {
        // Square uses smallest currency unit (cents for USD, etc.)
        // Most currencies use 100 as multiplier
        return (long)(amount * 100);
    }

    // Square API Models
    private class SquareCreatePaymentRequest
    {
        public string SourceId { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;
        public SquareMoney AmountMoney { get; set; } = null!;
        public string? ReferenceId { get; set; }
        public string? Note { get; set; }
        public string? LocationId { get; set; }
        public SquareApplicationDetails? ApplicationDetails { get; set; }
        public SquareTransferData? TransferData { get; set; }
    }

    private class SquareMoney
    {
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    private class SquareApplicationDetails
    {
        public string SquareProduct { get; set; } = string.Empty;
    }

    private class SquareTransferData
    {
        public string? Destination { get; set; }
    }

    private class SquareCancelPaymentRequest
    {
        public string IdempotencyKey { get; set; } = string.Empty;
    }

    private class SquarePaymentResponse
    {
        public SquarePayment? Payment { get; set; }
        public List<SquareError>? Errors { get; set; }
    }

    private class SquarePayment
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public SquareMoney? AmountMoney { get; set; }
        public string? ReceiptNumber { get; set; }
        public string? ReceiptUrl { get; set; }
        public string? OrderId { get; set; }
        public string? CreatedAt { get; set; }
        public string? UpdatedAt { get; set; }
    }

    private class SquareErrorResponse
    {
        public List<SquareError>? Errors { get; set; }
    }

    private class SquareError
    {
        public string? Category { get; set; }
        public string? Code { get; set; }
        public string? Detail { get; set; }
        public string? Field { get; set; }
    }
}

