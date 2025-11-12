using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class TelrPaymentProvider : IPaymentProvider, IPaymentCallbackProvider
{
    private readonly ILogger<TelrPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly TelrProviderConfiguration _config;

    public string ProviderName => "Telr";

    public TelrPaymentProvider(
        ILogger<TelrPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(TelrPaymentProvider));
        _config = new TelrProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Telr:BaseUrl"] ?? "https://secure.telr.com/gateway/",
            StoreId = configuration["PaymentProviders:Telr:StoreId"] ?? string.Empty,
            AuthKey = configuration["PaymentProviders:Telr:AuthKey"] ?? string.Empty,
            ReturnUrl = configuration["PaymentProviders:Telr:ReturnUrl"] ?? string.Empty,
            CancelUrl = configuration["PaymentProviders:Telr:CancelUrl"] ?? string.Empty,
            DeclineUrl = configuration["PaymentProviders:Telr:DeclineUrl"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Telr:IsTestMode", true)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Telr for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.StoreId) || string.IsNullOrEmpty(_config.AuthKey))
            {
                throw new InvalidOperationException("Telr StoreId and AuthKey must be configured");
            }

            // Telr uses Hosted Payment Page approach (no PCI certification needed)
            // We create an order and get a payment URL for redirect

            // Build order request
            var orderRequest = new TelrOrderRequest
            {
                Method = "create",
                Store = _config.StoreId,
                AuthKey = _config.AuthKey,
                Test = _config.IsTestMode ? 1 : 0,
                Order = new TelrOrder
                {
                    Id = request.OrderId,
                    Amount = request.Amount.Value.ToString("F2"),
                    Currency = request.Currency.Code,
                    Description = request.Metadata?.GetValueOrDefault("description") ?? $"Payment for order {request.OrderId}"
                },
                Return = new TelrReturnUrls
                {
                    Url = _config.ReturnUrl,
                    Cancel = _config.CancelUrl,
                    Decline = _config.DeclineUrl
                }
            };

            // Add customer information if available
            if (request.Metadata != null)
            {
                if (request.Metadata.TryGetValue("customer_name", out var customerName))
                {
                    orderRequest.Customer = new TelrCustomer
                    {
                        Name = new TelrName
                        {
                            First = customerName.Split(' ').FirstOrDefault() ?? customerName,
                            Last = customerName.Contains(' ') ? string.Join(" ", customerName.Split(' ').Skip(1)) : string.Empty
                        }
                    };
                }

                if (request.Metadata.TryGetValue("customer_email", out var customerEmail))
                {
                    if (orderRequest.Customer == null)
                    {
                        orderRequest.Customer = new TelrCustomer();
                    }
                    orderRequest.Customer.Email = customerEmail;
                }

                if (request.Metadata.TryGetValue("customer_phone", out var customerPhone))
                {
                    if (orderRequest.Customer == null)
                    {
                        orderRequest.Customer = new TelrCustomer();
                    }
                    orderRequest.Customer.Phone = customerPhone;
                }

                if (request.Metadata.TryGetValue("billing_address", out var billingAddress))
                {
                    if (orderRequest.Customer == null)
                    {
                        orderRequest.Customer = new TelrCustomer();
                    }
                    if (orderRequest.Customer.Address == null)
                    {
                        orderRequest.Customer.Address = new TelrAddress();
                    }
                    orderRequest.Customer.Address.Line1 = billingAddress;
                }

                if (request.Metadata.TryGetValue("billing_city", out var billingCity))
                {
                    if (orderRequest.Customer == null)
                    {
                        orderRequest.Customer = new TelrCustomer();
                    }
                    if (orderRequest.Customer.Address == null)
                    {
                        orderRequest.Customer.Address = new TelrAddress();
                    }
                    orderRequest.Customer.Address.City = billingCity;
                }

                if (request.Metadata.TryGetValue("billing_country", out var billingCountry))
                {
                    if (orderRequest.Customer == null)
                    {
                        orderRequest.Customer = new TelrCustomer();
                    }
                    if (orderRequest.Customer.Address == null)
                    {
                        orderRequest.Customer.Address = new TelrAddress();
                    }
                    orderRequest.Customer.Address.Country = billingCountry;
                }

                if (request.Metadata.TryGetValue("billing_postal_code", out var billingPostalCode))
                {
                    if (orderRequest.Customer == null)
                    {
                        orderRequest.Customer = new TelrCustomer();
                    }
                    if (orderRequest.Customer.Address == null)
                    {
                        orderRequest.Customer.Address = new TelrAddress();
                    }
                    orderRequest.Customer.Address.Postcode = billingPostalCode;
                }
            }

            // Handle split payments
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                // Telr doesn't natively support split payments
                // You would need to process two separate transactions or handle it at application level
                _logger.LogWarning("Telr doesn't natively support split payments. Processing as single payment.");
            }

            var jsonContent = JsonSerializer.Serialize(orderRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var apiUrl = $"{_config.BaseUrl}order.json";
            _logger.LogInformation("Creating Telr payment order for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var orderResponse = JsonSerializer.Deserialize<TelrOrderResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderResponse?.Order != null)
                {
                    var order = orderResponse.Order;
                    var statusCode = order.Status?.Code ?? 0;
                    
                    // Status code 2 = Payment page URL is ready
                    // Status code 3 = Order created successfully
                    var isSuccess = statusCode == 2 || statusCode == 3;
                    var paymentUrl = order.Url;

                    if (isSuccess && !string.IsNullOrEmpty(paymentUrl))
                    {
                        _logger.LogInformation("Telr payment order created successfully. Order ID: {OrderId}, Payment URL: {PaymentUrl}",
                            order.Id, paymentUrl);

                        return new PaymentResult(
                            Success: true,
                            TransactionId: order.Id ?? request.OrderId,
                            FailureReason: null,
                            ProviderMetadata: new Dictionary<string, string>
                            {
                                { "Provider", "Telr" },
                                { "OrderId", order.Id ?? request.OrderId },
                                { "PaymentUrl", paymentUrl },
                                { "Status", order.Status?.Code.ToString() ?? string.Empty },
                                { "StatusText", order.Status?.Text ?? string.Empty },
                                { "StatusMessage", order.Status?.Message ?? string.Empty },
                                { "Amount", order.Amount ?? string.Empty },
                                { "Currency", order.Currency ?? string.Empty },
                                { "TestMode", _config.IsTestMode ? "1" : "0" },
                                { "ProcessedAt", DateTime.UtcNow.ToString("O") }
                            });
                    }
                    else
                    {
                        var errorMessage = order.Status?.Text ?? order.Status?.Message ?? "Unknown error from Telr";
                        _logger.LogWarning("Telr payment order creation failed: {ErrorMessage}", errorMessage);

                        return new PaymentResult(
                            Success: false,
                            TransactionId: order.Id ?? request.OrderId,
                            FailureReason: errorMessage,
                            ProviderMetadata: new Dictionary<string, string>
                            {
                                { "Status", order.Status?.Code.ToString() ?? string.Empty },
                                { "StatusText", order.Status?.Text ?? string.Empty },
                                { "StatusMessage", order.Status?.Message ?? string.Empty }
                            });
                    }
                }
            }

            // Handle errors
            _logger.LogError("Telr API returned error status {StatusCode}: {Response}",
                response.StatusCode, responseString);

            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Telr API error: {response.StatusCode}",
                ProviderMetadata: new Dictionary<string, string>
                {
                    { "StatusCode", response.StatusCode.ToString() },
                    { "Response", responseString }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telr payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Telr payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Verifies a payment callback from Telr (implements IPaymentCallbackProvider)
    /// </summary>
    public async Task<PaymentResult> VerifyCallbackAsync(
        Dictionary<string, string> callbackData,
        CancellationToken cancellationToken = default)
    {
        if (!callbackData.TryGetValue("order_id", out var orderId) && 
            !callbackData.TryGetValue("orderId", out orderId) &&
            !callbackData.TryGetValue("OrderId", out orderId))
        {
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: "Order ID is required in callback data",
                ProviderMetadata: null);
        }

        return await GetOrderStatusAsync(orderId!, cancellationToken);
    }

    /// <summary>
    /// Retrieves order status from Telr
    /// </summary>
    public async Task<PaymentResult> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var statusRequest = new TelrStatusRequest
            {
                Method = "check",
                Store = _config.StoreId,
                AuthKey = _config.AuthKey,
                Test = _config.IsTestMode ? 1 : 0,
                Order = new TelrOrderReference
                {
                    Id = orderId
                }
            };

            var jsonContent = JsonSerializer.Serialize(statusRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"{_config.BaseUrl}order.json";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var orderResponse = JsonSerializer.Deserialize<TelrOrderResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderResponse?.Order != null)
                {
                    var order = orderResponse.Order;
                    var statusCode = order.Status?.Code ?? 0;
                    
                    // Status codes:
                    // 2 = Payment page ready
                    // 3 = Order created
                    // 4 = Payment authorized
                    // 5 = Payment captured
                    // -1 = Payment failed/cancelled
                    var isSuccess = statusCode == 4 || statusCode == 5;

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: order.Id ?? orderId,
                        FailureReason: isSuccess ? null : order.Status?.Text ?? order.Status?.Message,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Status", order.Status?.Code.ToString() ?? string.Empty },
                            { "StatusText", order.Status?.Text ?? string.Empty },
                            { "StatusMessage", order.Status?.Message ?? string.Empty },
                            { "Amount", order.Amount ?? string.Empty },
                            { "Currency", order.Currency ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: orderId,
                FailureReason: "Failed to retrieve order status",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Telr order status for {OrderId}", orderId);
            return new PaymentResult(
                Success: false,
                TransactionId: orderId,
                FailureReason: $"Status check failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    // Telr API Models
    private class TelrOrderRequest
    {
        public string Method { get; set; } = "create";
        public string Store { get; set; } = string.Empty;
        public string AuthKey { get; set; } = string.Empty;
        public int Test { get; set; }
        public TelrOrder Order { get; set; } = null!;
        public TelrReturnUrls Return { get; set; } = null!;
        public TelrCustomer? Customer { get; set; }
    }

    private class TelrOrder
    {
        public string Id { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private class TelrOrderReference
    {
        public string Id { get; set; } = string.Empty;
    }

    private class TelrReturnUrls
    {
        public string Url { get; set; } = string.Empty;
        public string? Cancel { get; set; }
        public string? Decline { get; set; }
    }

    private class TelrCustomer
    {
        public TelrName? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public TelrAddress? Address { get; set; }
    }

    private class TelrName
    {
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
    }

    private class TelrAddress
    {
        public string? Line1 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Postcode { get; set; }
    }

    private class TelrStatusRequest
    {
        public string Method { get; set; } = "check";
        public string Store { get; set; } = string.Empty;
        public string AuthKey { get; set; } = string.Empty;
        public int Test { get; set; }
        public TelrOrderReference Order { get; set; } = null!;
    }

    private class TelrOrderResponse
    {
        public TelrOrderDetails? Order { get; set; }
    }

    private class TelrOrderDetails
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Amount { get; set; }
        public string? Currency { get; set; }
        public TelrOrderStatus? Status { get; set; }
    }

    private class TelrOrderStatus
    {
        public int? Code { get; set; }
        public string? Text { get; set; }
        public string? Message { get; set; }
    }
}

