using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class HelcimPaymentProvider : IPaymentProvider
{
    private readonly ILogger<HelcimPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly HelcimProviderConfiguration _config;

    public string ProviderName => "Helcim";

    public HelcimPaymentProvider(
        ILogger<HelcimPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(HelcimPaymentProvider));
        _config = new HelcimProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Helcim:BaseUrl"] ?? "https://api.helcim.com/v2/",
            ApiToken = configuration["PaymentProviders:Helcim:ApiToken"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Helcim:IsTestMode", true)
        };

        // Set up authentication header
        // Helcim uses API token in the Authorization header
        if (!string.IsNullOrEmpty(_config.ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Add("api-token", _config.ApiToken);
        }

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Helcim for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.ApiToken))
            {
                throw new InvalidOperationException("Helcim ApiToken must be configured");
            }

            // Helcim requires payment method details
            // Check if we have card token or card details in metadata
            string? cardToken = null;
            var hasCardToken = request.Metadata?.TryGetValue("card_token", out cardToken) == true && !string.IsNullOrEmpty(cardToken);
            var hasCardDetails = request.Metadata?.ContainsKey("card_number") == true;

            if (!hasCardToken && !hasCardDetails)
            {
                throw new InvalidOperationException("Helcim requires either card_token or card details (card_number, card_expiry, card_cvv) in metadata");
            }

            // Convert amount to string with 2 decimal places (Helcim expects string)
            var amountString = request.Amount.Value.ToString("F2");

            // Create payment request
            var paymentRequest = new HelcimPaymentRequest
            {
                PaymentType = "purchase", // Purchase payment type
                Amount = amountString,
                Currency = request.Currency.Code,
                InvoiceNumber = request.OrderId
            };

            // Add payment method
            if (hasCardToken && cardToken != null)
            {
                paymentRequest.CardToken = cardToken;
            }
            else
            {
                // Card details from metadata
                paymentRequest.CardNumber = request.Metadata!["card_number"];
                paymentRequest.CardExpiry = request.Metadata["card_expiry"]; // Format: MMYY or MM/YY
                paymentRequest.CardCvv = request.Metadata["card_cvv"];
            }

            // Add customer information if available
            if (request.Metadata != null)
            {
                if (request.Metadata.TryGetValue("customer_code", out var customerCode))
                {
                    paymentRequest.CustomerCode = customerCode;
                }

                if (request.Metadata.TryGetValue("billing_contact_name", out var contactName))
                {
                    paymentRequest.BillingContactName = contactName;
                }

                if (request.Metadata.TryGetValue("billing_contact_email", out var contactEmail))
                {
                    paymentRequest.BillingContactEmail = contactEmail;
                }

                if (request.Metadata.TryGetValue("billing_contact_phone", out var contactPhone))
                {
                    paymentRequest.BillingContactPhone = contactPhone;
                }

                if (request.Metadata.TryGetValue("billing_street1", out var street1))
                {
                    paymentRequest.BillingStreet1 = street1;
                }

                if (request.Metadata.TryGetValue("billing_city", out var city))
                {
                    paymentRequest.BillingCity = city;
                }

                if (request.Metadata.TryGetValue("billing_province", out var province))
                {
                    paymentRequest.BillingProvince = province;
                }

                if (request.Metadata.TryGetValue("billing_postal_code", out var postalCode))
                {
                    paymentRequest.BillingPostalCode = postalCode;
                }

                if (request.Metadata.TryGetValue("billing_country", out var country))
                {
                    paymentRequest.BillingCountry = country;
                }
            }

            // Handle split payments
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                // Helcim doesn't natively support split payments in a single transaction
                // You would need to process two separate transactions or handle it at application level
                _logger.LogWarning("Helcim doesn't natively support split payments. Processing as single payment.");
            }

            var jsonContent = JsonSerializer.Serialize(paymentRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var apiUrl = $"{_config.BaseUrl}payment/credit-card";
            _logger.LogInformation("Creating Helcim payment for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentResponse = JsonSerializer.Deserialize<HelcimPaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse != null)
                {
                    var isSuccess = paymentResponse.Response == 1 && paymentResponse.ResponseMessage?.ToUpper() == "APPROVAL";
                    var failureReason = !isSuccess ? paymentResponse.ResponseMessage : null;

                    _logger.LogInformation("Helcim payment processed. Transaction ID: {TransactionId}, Response: {Response}, Message: {Message}",
                        paymentResponse.TransactionId, paymentResponse.Response, paymentResponse.ResponseMessage);

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentResponse.TransactionId?.ToString() ?? string.Empty,
                        FailureReason: failureReason,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "Helcim" },
                            { "TransactionId", paymentResponse.TransactionId?.ToString() ?? string.Empty },
                            { "Response", paymentResponse.Response?.ToString() ?? string.Empty },
                            { "ResponseMessage", paymentResponse.ResponseMessage ?? string.Empty },
                            { "AvsResponse", paymentResponse.AvsResponse ?? string.Empty },
                            { "CvvResponse", paymentResponse.CvvResponse ?? string.Empty },
                            { "InvoiceNumber", paymentResponse.InvoiceNumber ?? string.Empty },
                            { "Amount", paymentResponse.Amount ?? string.Empty },
                            { "Currency", paymentResponse.Currency ?? string.Empty },
                            { "DateCreated", paymentResponse.DateCreated ?? string.Empty },
                            { "IsTestMode", _config.IsTestMode.ToString() }
                        });
                }
            }

            // Handle Helcim errors
            var errorResponse = JsonSerializer.Deserialize<HelcimErrorResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var errorMessage = "Unknown error from Helcim";
            if (errorResponse?.Errors != null && errorResponse.Errors.Count > 0)
            {
                errorMessage = string.Join("; ", errorResponse.Errors.Select(e => $"{e.Field}: {e.Message}"));
            }
            else if (!string.IsNullOrEmpty(errorResponse?.Message))
            {
                errorMessage = errorResponse.Message;
            }
            else
            {
                errorMessage = $"Helcim API error: {response.StatusCode}";
            }

            _logger.LogError("Helcim payment creation failed: {ErrorMessage}", errorMessage);

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
            _logger.LogError(ex, "Error processing Helcim payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Helcim payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Preauthorizes a payment (authorizes but doesn't capture)
    /// </summary>
    public async Task<PaymentResult> PreauthorizePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Similar to ProcessPaymentAsync but with paymentType = "preauth"
            var amountString = request.Amount.Value.ToString("F2");

            var paymentRequest = new HelcimPaymentRequest
            {
                PaymentType = "preauth",
                Amount = amountString,
                Currency = request.Currency.Code,
                InvoiceNumber = request.OrderId
            };

            // Add payment method (same as ProcessPaymentAsync)
            if (request.Metadata?.TryGetValue("card_token", out var cardToken) == true && !string.IsNullOrEmpty(cardToken))
            {
                paymentRequest.CardToken = cardToken;
            }
            else if (request.Metadata != null)
            {
                paymentRequest.CardNumber = request.Metadata.GetValueOrDefault("card_number");
                paymentRequest.CardExpiry = request.Metadata.GetValueOrDefault("card_expiry");
                paymentRequest.CardCvv = request.Metadata.GetValueOrDefault("card_cvv");
            }

            var jsonContent = JsonSerializer.Serialize(paymentRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"{_config.BaseUrl}payment/credit-card";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentResponse = JsonSerializer.Deserialize<HelcimPaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse != null)
                {
                    var isSuccess = paymentResponse.Response == 1 && paymentResponse.ResponseMessage?.ToUpper() == "APPROVAL";
                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentResponse.TransactionId?.ToString() ?? string.Empty,
                        FailureReason: isSuccess ? null : paymentResponse.ResponseMessage,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "PaymentType", "preauth" },
                            { "TransactionId", paymentResponse.TransactionId?.ToString() ?? string.Empty },
                            { "ResponseMessage", paymentResponse.ResponseMessage ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: "Preauthorization failed",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preauthorizing Helcim payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Preauthorization failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Captures a preauthorized payment
    /// </summary>
    public async Task<PaymentResult> CapturePaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var captureRequest = new HelcimCaptureRequest
            {
                TransactionId = transactionId
            };

            if (amount.HasValue)
            {
                captureRequest.Amount = amount.Value.ToString("F2");
            }

            var jsonContent = JsonSerializer.Serialize(captureRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"{_config.BaseUrl}payment/capture";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentResponse = JsonSerializer.Deserialize<HelcimPaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse != null)
                {
                    var isSuccess = paymentResponse.Response == 1 && paymentResponse.ResponseMessage?.ToUpper() == "APPROVAL";
                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentResponse.TransactionId?.ToString() ?? string.Empty,
                        FailureReason: isSuccess ? null : paymentResponse.ResponseMessage,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "PaymentType", "capture" },
                            { "OriginalTransactionId", transactionId },
                            { "TransactionId", paymentResponse.TransactionId?.ToString() ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: transactionId,
                FailureReason: "Capture failed",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing Helcim payment {TransactionId}", transactionId);
            return new PaymentResult(
                Success: false,
                TransactionId: transactionId,
                FailureReason: $"Capture failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Refunds a payment
    /// </summary>
    public async Task<PaymentResult> RefundPaymentAsync(string transactionId, decimal? amount = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var refundRequest = new HelcimRefundRequest
            {
                TransactionId = transactionId
            };

            if (amount.HasValue)
            {
                refundRequest.Amount = amount.Value.ToString("F2");
            }

            var jsonContent = JsonSerializer.Serialize(refundRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"{_config.BaseUrl}payment/refund";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentResponse = JsonSerializer.Deserialize<HelcimPaymentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentResponse != null)
                {
                    var isSuccess = paymentResponse.Response == 1 && paymentResponse.ResponseMessage?.ToUpper() == "APPROVAL";
                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentResponse.TransactionId?.ToString() ?? string.Empty,
                        FailureReason: isSuccess ? null : paymentResponse.ResponseMessage,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "PaymentType", "refund" },
                            { "OriginalTransactionId", transactionId },
                            { "TransactionId", paymentResponse.TransactionId?.ToString() ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: transactionId,
                FailureReason: "Refund failed",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding Helcim payment {TransactionId}", transactionId);
            return new PaymentResult(
                Success: false,
                TransactionId: transactionId,
                FailureReason: $"Refund failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Gets transaction details
    /// </summary>
    public async Task<PaymentResult> GetTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiUrl = $"{_config.BaseUrl}transaction/{transactionId}";
            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var transactionResponse = JsonSerializer.Deserialize<HelcimTransactionResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (transactionResponse?.Transaction != null)
                {
                    var transaction = transactionResponse.Transaction;
                    var isSuccess = transaction.Response == 1 && transaction.ResponseMessage?.ToUpper() == "APPROVAL";

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: transaction.TransactionId?.ToString() ?? string.Empty,
                        FailureReason: isSuccess ? null : transaction.ResponseMessage,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "TransactionId", transaction.TransactionId?.ToString() ?? string.Empty },
                            { "PaymentType", transaction.PaymentType ?? string.Empty },
                            { "Status", transaction.Status ?? string.Empty },
                            { "Amount", transaction.Amount ?? string.Empty },
                            { "Currency", transaction.Currency ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: transactionId,
                FailureReason: "Failed to retrieve transaction",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Helcim transaction {TransactionId}", transactionId);
            return new PaymentResult(
                Success: false,
                TransactionId: transactionId,
                FailureReason: $"Retrieval failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    // Helcim API Models
    private class HelcimPaymentRequest
    {
        public string PaymentType { get; set; } = "purchase";
        public string Amount { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }
        public string? CardToken { get; set; }
        public string? CardNumber { get; set; }
        public string? CardExpiry { get; set; }
        public string? CardCvv { get; set; }
        public string? CustomerCode { get; set; }
        public string? BillingContactName { get; set; }
        public string? BillingContactEmail { get; set; }
        public string? BillingContactPhone { get; set; }
        public string? BillingStreet1 { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingProvince { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
    }

    private class HelcimCaptureRequest
    {
        public string TransactionId { get; set; } = string.Empty;
        public string? Amount { get; set; }
    }

    private class HelcimRefundRequest
    {
        public string TransactionId { get; set; } = string.Empty;
        public string? Amount { get; set; }
    }

    private class HelcimPaymentResponse
    {
        public int? Response { get; set; }
        public string? ResponseMessage { get; set; }
        public int? TransactionId { get; set; }
        public string? AvsResponse { get; set; }
        public string? CvvResponse { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? Amount { get; set; }
        public string? Currency { get; set; }
        public string? DateCreated { get; set; }
    }

    private class HelcimTransactionResponse
    {
        public HelcimTransaction? Transaction { get; set; }
    }

    private class HelcimTransaction
    {
        public int? TransactionId { get; set; }
        public string? PaymentType { get; set; }
        public string? Status { get; set; }
        public int? Response { get; set; }
        public string? ResponseMessage { get; set; }
        public string? Amount { get; set; }
        public string? Currency { get; set; }
    }

    private class HelcimErrorResponse
    {
        public string? Message { get; set; }
        public List<HelcimError>? Errors { get; set; }
    }

    private class HelcimError
    {
        public string? Field { get; set; }
        public string? Message { get; set; }
    }
}

