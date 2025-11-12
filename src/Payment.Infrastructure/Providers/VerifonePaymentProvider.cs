using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class VerifonePaymentProvider : IPaymentProvider
{
    private readonly ILogger<VerifonePaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly VerifoneProviderConfiguration _config;

    public string ProviderName => "Verifone";

    public VerifonePaymentProvider(
        ILogger<VerifonePaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(VerifonePaymentProvider));
        _config = new VerifoneProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Verifone:BaseUrl"] ?? "https://api.2checkout.com/rest/6.0/",
            SellerId = configuration["PaymentProviders:Verifone:SellerId"] ?? string.Empty,
            SecretKey = configuration["PaymentProviders:Verifone:SecretKey"] ?? string.Empty,
            PublishableKey = configuration["PaymentProviders:Verifone:PublishableKey"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Verifone:IsTestMode", true)
        };

        // Set up authentication header
        if (!string.IsNullOrEmpty(_config.SecretKey))
        {
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.SellerId}:{_config.SecretKey}"));
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Verifone (2Checkout) for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.SellerId) || string.IsNullOrEmpty(_config.SecretKey))
            {
                throw new InvalidOperationException("Verifone SellerId and SecretKey must be configured");
            }

            // Verifone requires token from 2co.js library
            if (request.Metadata == null || !request.Metadata.TryGetValue("token", out var token) || string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Verifone requires 'token' in metadata. This should be obtained from 2co.js JavaScript library.");
            }

            // Create order request
            var orderRequest = new VerifoneOrderRequest
            {
                SellerId = _config.SellerId,
                Token = token,
                Currency = request.Currency.Code,
                Items = new List<VerifoneOrderItem>
                {
                    new VerifoneOrderItem
                    {
                        Name = request.Metadata?.GetValueOrDefault("product_name") ?? $"Order {request.OrderId}",
                        Description = request.Metadata?.GetValueOrDefault("description") ?? $"Payment for order {request.OrderId}",
                        Quantity = 1,
                        Price = request.Amount.Value.ToString("F2"),
                        Type = "PRODUCT",
                        Tangible = 0
                    }
                },
                BillingDetails = new VerifoneBillingDetails
                {
                    Address1 = request.Metadata?.GetValueOrDefault("billing_address") ?? string.Empty,
                    City = request.Metadata?.GetValueOrDefault("billing_city") ?? string.Empty,
                    State = request.Metadata?.GetValueOrDefault("billing_state") ?? string.Empty,
                    Zip = request.Metadata?.GetValueOrDefault("billing_postal_code") ?? string.Empty,
                    Country = request.Metadata?.GetValueOrDefault("billing_country") ?? string.Empty,
                    Email = request.Metadata?.GetValueOrDefault("customer_email") ?? string.Empty,
                    FirstName = request.Metadata?.GetValueOrDefault("customer_first_name") ?? string.Empty,
                    LastName = request.Metadata?.GetValueOrDefault("customer_last_name") ?? string.Empty
                }
            };

            // Handle split payments
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                // Verifone doesn't natively support split payments
                // You would need to process two separate transactions or handle it at application level
                _logger.LogWarning("Verifone doesn't natively support split payments. Processing as single payment.");
            }

            var jsonContent = JsonSerializer.Serialize(orderRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var apiUrl = $"{_config.BaseUrl}orders/";
            _logger.LogInformation("Creating Verifone order for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var orderResponse = JsonSerializer.Deserialize<VerifoneOrderResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderResponse != null && orderResponse.Order != null)
                {
                    var order = orderResponse.Order;
                    var isSuccess = order.Status == "AUTHRECEIVED" || order.Status == "COMPLETE";

                    _logger.LogInformation("Verifone order created successfully. Order Number: {OrderNumber}, Status: {Status}",
                        order.OrderNumber, order.Status);

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: order.OrderNumber ?? request.OrderId,
                        FailureReason: isSuccess ? null : $"Order status: {order.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "Verifone" },
                            { "OrderNumber", order.OrderNumber ?? string.Empty },
                            { "Status", order.Status ?? string.Empty },
                            { "RefNo", order.RefNo ?? string.Empty },
                            { "Amount", order.Amount?.ToString() ?? string.Empty },
                            { "Currency", order.Currency ?? string.Empty },
                            { "ProcessedAt", DateTime.UtcNow.ToString("O") },
                            { "IsTestMode", _config.IsTestMode.ToString() }
                        });
                }
            }

            // Handle errors
            var errorResponse = JsonSerializer.Deserialize<VerifoneErrorResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var errorMessage = errorResponse?.Errors?.FirstOrDefault()?.Message ?? $"Verifone API error: {response.StatusCode}";
            _logger.LogError("Verifone order creation failed: {ErrorMessage}", errorMessage);

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
            _logger.LogError(ex, "Error processing Verifone payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Verifone payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Validates IPN signature (for webhook handling)
    /// </summary>
    public bool ValidateIpnSignature(Dictionary<string, string> ipnData, string signature, string date)
    {
        try
        {
            var calculatedSignature = CalculateIpnSignature(ipnData, _config.SecretKey, date);
            return string.Equals(calculatedSignature, signature, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating IPN signature");
            return false;
        }
    }

    /// <summary>
    /// Calculates IPN signature according to Verifone specification
    /// </summary>
    private string CalculateIpnSignature(Dictionary<string, string> requestPayloadValues, string secretKey, string currentDate)
    {
        var plainDataToHash = new StringBuilder();

        // Process IPN fields in specific order
        var ipnFields = new[] { "IPN_PID[]", "IPN_PNAME[]", "IPN_DATE" };
        
        foreach (var field in ipnFields)
        {
            if (requestPayloadValues.TryGetValue(field, out var value))
            {
                plainDataToHash.Append(value.Length);
                plainDataToHash.Append(value);
            }
        }

        // Add date
        plainDataToHash.Append(currentDate.Length);
        plainDataToHash.Append(currentDate);

        // Calculate HMAC SHA-256
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var dataBytes = Encoding.UTF8.GetBytes(plainDataToHash.ToString());
        
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
        // Convert to hex string
        var hexString = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            hexString.AppendFormat("{0:x2}", b);
        }

        return hexString.ToString();
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

    // Verifone API Models
    private class VerifoneOrderRequest
    {
        public string SellerId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public List<VerifoneOrderItem> Items { get; set; } = new();
        public VerifoneBillingDetails BillingDetails { get; set; } = null!;
    }

    private class VerifoneOrderItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Price { get; set; } = string.Empty;
        public string Type { get; set; } = "PRODUCT";
        public int Tangible { get; set; }
    }

    private class VerifoneBillingDetails
    {
        public string Address1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    private class VerifoneOrderResponse
    {
        public VerifoneOrder? Order { get; set; }
    }

    private class VerifoneOrder
    {
        public string? OrderNumber { get; set; }
        public string? RefNo { get; set; }
        public string? Status { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
    }

    private class VerifoneErrorResponse
    {
        public List<VerifoneError>? Errors { get; set; }
    }

    private class VerifoneError
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }
}

