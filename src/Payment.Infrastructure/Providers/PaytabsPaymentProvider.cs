using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class PaytabsPaymentProvider : IPaymentProvider
{
    private readonly ILogger<PaytabsPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly PaytabsProviderConfiguration _config;

    public string ProviderName => "Paytabs";

    public PaytabsPaymentProvider(
        ILogger<PaytabsPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(PaytabsPaymentProvider));
        _config = new PaytabsProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Paytabs:BaseUrl"] ?? "https://secure.paytabs.com/",
            ProfileId = configuration["PaymentProviders:Paytabs:ProfileId"] ?? string.Empty,
            ServerKey = configuration["PaymentProviders:Paytabs:ServerKey"] ?? string.Empty,
            ClientKey = configuration["PaymentProviders:Paytabs:ClientKey"] ?? string.Empty,
            ReturnUrl = configuration["PaymentProviders:Paytabs:ReturnUrl"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Paytabs:IsTestMode", true)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Authorization", _config.ServerKey);
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Paytabs for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.ProfileId) || string.IsNullOrEmpty(_config.ServerKey))
            {
                throw new InvalidOperationException("Paytabs ProfileId and ServerKey must be configured");
            }

            // Paytabs uses Hosted Payment Page (7-step process)
            // Step 1: Create payment page

            var amountString = request.Amount.Value.ToString("F2");

            var paymentPageRequest = new PaytabsPaymentPageRequest
            {
                ProfileId = int.Parse(_config.ProfileId),
                TranType = "sale",
                TranClass = "ecom",
                CartId = request.OrderId,
                CartDescription = request.Metadata?.GetValueOrDefault("description") ?? $"Payment for order {request.OrderId}",
                CartCurrency = request.Currency.Code,
                CartAmount = amountString,
                Callback = _config.ReturnUrl,
                Return = _config.ReturnUrl
            };

            // Add customer information
            if (request.Metadata != null)
            {
                if (request.Metadata.TryGetValue("customer_name", out var customerName))
                {
                    var nameParts = customerName.Split(' ', 2);
                    paymentPageRequest.CustomerDetails = new PaytabsCustomerDetails
                    {
                        Name = customerName,
                        Email = request.Metadata.GetValueOrDefault("customer_email") ?? string.Empty,
                        Phone = request.Metadata.GetValueOrDefault("customer_phone") ?? string.Empty,
                        Street1 = request.Metadata.GetValueOrDefault("billing_address") ?? string.Empty,
                        City = request.Metadata.GetValueOrDefault("billing_city") ?? string.Empty,
                        State = request.Metadata.GetValueOrDefault("billing_state") ?? string.Empty,
                        Country = request.Metadata.GetValueOrDefault("billing_country") ?? string.Empty,
                        Zip = request.Metadata.GetValueOrDefault("billing_postal_code") ?? string.Empty
                    };
                }
            }

            // Handle split payments
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                // Paytabs doesn't natively support split payments
                // You would need to process two separate transactions or handle it at application level
                _logger.LogWarning("Paytabs doesn't natively support split payments. Processing as single payment.");
            }

            var jsonContent = JsonSerializer.Serialize(paymentPageRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var apiUrl = $"{_config.BaseUrl}payment/request";
            _logger.LogInformation("Creating Paytabs payment page for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentPageResponse = JsonSerializer.Deserialize<PaytabsPaymentPageResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentPageResponse != null)
                {
                    var isSuccess = paymentPageResponse.ResponseCode == "4011" || paymentPageResponse.ResponseCode == "100";
                    var paymentUrl = paymentPageResponse.RedirectUrl ?? paymentPageResponse.PaymentUrl;

                    if (isSuccess && !string.IsNullOrEmpty(paymentUrl))
                    {
                        _logger.LogInformation("Paytabs payment page created successfully. Transaction Reference: {TranRef}, Payment URL: {PaymentUrl}",
                            paymentPageResponse.TranRef, paymentUrl);

                        return new PaymentResult(
                            Success: true,
                            TransactionId: paymentPageResponse.TranRef ?? request.OrderId,
                            FailureReason: null,
                            ProviderMetadata: new Dictionary<string, string>
                            {
                                { "Provider", "Paytabs" },
                                { "TranRef", paymentPageResponse.TranRef ?? string.Empty },
                                { "PaymentUrl", paymentUrl },
                                { "ResponseCode", paymentPageResponse.ResponseCode ?? string.Empty },
                                { "ResponseMessage", paymentPageResponse.ResponseMessage ?? string.Empty },
                                { "CartId", paymentPageResponse.CartId ?? string.Empty },
                                { "Amount", paymentPageResponse.CartAmount ?? string.Empty },
                                { "Currency", paymentPageResponse.CartCurrency ?? string.Empty },
                                { "ProcessedAt", DateTime.UtcNow.ToString("O") },
                                { "IsTestMode", _config.IsTestMode.ToString() }
                            });
                    }
                    else
                    {
                        var errorMessage = paymentPageResponse.ResponseMessage ?? "Unknown error from Paytabs";
                        _logger.LogWarning("Paytabs payment page creation failed: {ErrorMessage}", errorMessage);

                        return new PaymentResult(
                            Success: false,
                            TransactionId: paymentPageResponse.TranRef ?? request.OrderId,
                            FailureReason: errorMessage,
                            ProviderMetadata: new Dictionary<string, string>
                            {
                                { "ResponseCode", paymentPageResponse.ResponseCode ?? string.Empty },
                                { "ResponseMessage", paymentPageResponse.ResponseMessage ?? string.Empty }
                            });
                    }
                }
            }

            // Handle errors
            _logger.LogError("Paytabs API returned error status {StatusCode}: {Response}",
                response.StatusCode, responseString);

            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Paytabs API error: {response.StatusCode}",
                ProviderMetadata: new Dictionary<string, string>
                {
                    { "StatusCode", response.StatusCode.ToString() },
                    { "Response", responseString }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Paytabs payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Paytabs payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Queries transaction status
    /// </summary>
    public async Task<PaymentResult> QueryTransactionAsync(string tranRef, CancellationToken cancellationToken = default)
    {
        try
        {
            var queryRequest = new PaytabsQueryRequest
            {
                ProfileId = int.Parse(_config.ProfileId),
                TranRef = tranRef
            };

            var jsonContent = JsonSerializer.Serialize(queryRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"{_config.BaseUrl}payment/query";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var queryResponse = JsonSerializer.Deserialize<PaytabsQueryResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (queryResponse != null)
                {
                    var isSuccess = queryResponse.ResponseCode == "100" && queryResponse.PaymentResult == "A";
                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: queryResponse.TranRef ?? tranRef,
                        FailureReason: isSuccess ? null : queryResponse.ResponseMessage,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "ResponseCode", queryResponse.ResponseCode ?? string.Empty },
                            { "PaymentResult", queryResponse.PaymentResult ?? string.Empty },
                            { "ResponseMessage", queryResponse.ResponseMessage ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: tranRef,
                FailureReason: "Failed to query transaction",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Paytabs transaction {TranRef}", tranRef);
            return new PaymentResult(
                Success: false,
                TransactionId: tranRef,
                FailureReason: $"Query failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    // Paytabs API Models
    private class PaytabsPaymentPageRequest
    {
        public int ProfileId { get; set; }
        public string TranType { get; set; } = "sale";
        public string TranClass { get; set; } = "ecom";
        public string CartId { get; set; } = string.Empty;
        public string CartDescription { get; set; } = string.Empty;
        public string CartCurrency { get; set; } = string.Empty;
        public string CartAmount { get; set; } = string.Empty;
        public string Callback { get; set; } = string.Empty;
        public string Return { get; set; } = string.Empty;
        public PaytabsCustomerDetails? CustomerDetails { get; set; }
    }

    private class PaytabsCustomerDetails
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Street1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
    }

    private class PaytabsQueryRequest
    {
        public int ProfileId { get; set; }
        public string TranRef { get; set; } = string.Empty;
    }

    private class PaytabsPaymentPageResponse
    {
        public string? TranRef { get; set; }
        public string? RedirectUrl { get; set; }
        public string? PaymentUrl { get; set; }
        public string? ResponseCode { get; set; }
        public string? ResponseMessage { get; set; }
        public string? CartId { get; set; }
        public string? CartAmount { get; set; }
        public string? CartCurrency { get; set; }
    }

    private class PaytabsQueryResponse
    {
        public string? TranRef { get; set; }
        public string? ResponseCode { get; set; }
        public string? ResponseMessage { get; set; }
        public string? PaymentResult { get; set; }
    }
}

