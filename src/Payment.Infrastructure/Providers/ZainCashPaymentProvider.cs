using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class ZainCashPaymentProvider : IPaymentProvider, IPaymentCallbackProvider
{
    private readonly ILogger<ZainCashPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly ZainCashProviderConfiguration _config;
    private readonly IExchangeRateService? _exchangeRateService;

    public string ProviderName => "ZainCash";

    public ZainCashPaymentProvider(
        ILogger<ZainCashPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IExchangeRateService? exchangeRateService = null)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(ZainCashPaymentProvider));
        _exchangeRateService = exchangeRateService;
        _config = new ZainCashProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:ZainCash:BaseUrl"] ?? "https://api.zaincash.iq/",
            MerchantId = configuration["PaymentProviders:ZainCash:MerchantId"] ?? string.Empty,
            MerchantSecret = configuration["PaymentProviders:ZainCash:MerchantSecret"] ?? string.Empty,
            Msisdn = configuration["PaymentProviders:ZainCash:Msisdn"] ?? string.Empty,
            RedirectUrl = configuration["PaymentProviders:ZainCash:RedirectUrl"] ?? string.Empty,
            ServiceType = configuration["PaymentProviders:ZainCash:ServiceType"] ?? "Payment",
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:ZainCash:IsTestMode", true)
        };
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with ZainCash for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.MerchantId) || string.IsNullOrEmpty(_config.MerchantSecret))
            {
                throw new InvalidOperationException("ZainCash MerchantId and MerchantSecret must be configured");
            }

            if (string.IsNullOrEmpty(_config.Msisdn))
            {
                throw new InvalidOperationException("ZainCash Msisdn (wallet phone number) must be configured");
            }

            // Convert amount to IQD if needed (ZainCash primarily uses IQD)
            var amount = request.Currency.Code == "IQD" 
                ? request.Amount.Value 
                : await ConvertCurrencyAsync(request.Amount.Value, request.Currency.Code, "IQD", cancellationToken);

            // Create JWT token for payment initialization
            var transactionData = new
            {
                amount = amount,
                serviceType = _config.ServiceType,
                msisdn = _config.Msisdn,
                orderId = request.OrderId,
                redirectUrl = _config.RedirectUrl,
                iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                exp = DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds()
            };

            var token = GenerateJwtToken(transactionData, _config.MerchantSecret);
            _logger.LogDebug("Generated JWT token for ZainCash payment initialization");

            // Determine API endpoint based on test/live mode
            var apiBaseUrl = _config.IsTestMode 
                ? "https://test.zaincash.iq" 
                : "https://api.zaincash.iq";
            
            var initEndpoint = $"{apiBaseUrl}/transaction/init";

            // Prepare form data
            var formData = new List<KeyValuePair<string, string>>
            {
                new("token", token),
                new("merchantId", _config.MerchantId),
                new("lang", "en")
            };

            var content = new FormUrlEncodedContent(formData);

            // Send payment initialization request
            _logger.LogInformation("Sending payment initialization request to ZainCash for order {OrderId}", request.OrderId);
            var response = await _httpClient.PostAsync(initEndpoint, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ZainCash API returned error status {StatusCode}: {Response}",
                    response.StatusCode, responseString);
                return new PaymentResult(
                    Success: false,
                    TransactionId: null,
                    FailureReason: $"ZainCash API error: {response.StatusCode}",
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "StatusCode", response.StatusCode.ToString() },
                        { "Response", responseString }
                    });
            }

            // Parse response
            var responseObject = JsonSerializer.Deserialize<ZainCashInitResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (responseObject == null)
            {
                throw new InvalidOperationException("Failed to parse ZainCash response");
            }

            if (responseObject.Status?.ToLower() == "success" && !string.IsNullOrEmpty(responseObject.Id))
            {
                var transactionId = responseObject.Id;
                var paymentUrl = $"{apiBaseUrl}/transaction/pay?id={transactionId}";

                _logger.LogInformation("ZainCash payment initialized successfully. Transaction ID: {TransactionId}, Payment URL: {PaymentUrl}",
                    transactionId, paymentUrl);

                // Handle split payment if required
                if (request.SplitPayment != null)
                {
                    _logger.LogInformation("Split payment required: System={SystemShare}, Owner={OwnerShare}",
                        request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);
                    
                    // Note: ZainCash doesn't natively support split payments
                    // You would need to process two separate transactions or handle it at the application level
                    // For now, we'll log it and include it in metadata
                }

                return new PaymentResult(
                    Success: true,
                    TransactionId: transactionId,
                    FailureReason: null,
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "Provider", "ZainCash" },
                        { "ProviderTransactionId", transactionId },
                        { "PaymentUrl", paymentUrl },
                        { "Status", responseObject.Status },
                        { "ProcessedAt", DateTime.UtcNow.ToString("O") },
                        { "IsTestMode", _config.IsTestMode.ToString() }
                    });
            }
            else
            {
                var errorMessage = responseObject.Message ?? "Unknown error from ZainCash";
                _logger.LogWarning("ZainCash payment initialization failed: {Message}", errorMessage);
                
                return new PaymentResult(
                    Success: false,
                    TransactionId: null,
                    FailureReason: errorMessage,
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "Status", responseObject.Status ?? "unknown" },
                        { "Message", errorMessage }
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ZainCash payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"ZainCash payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Verifies a payment callback from ZainCash (implements IPaymentCallbackProvider)
    /// </summary>
    public Task<PaymentResult> VerifyCallbackAsync(
        Dictionary<string, string> callbackData,
        CancellationToken cancellationToken = default)
    {
        if (!callbackData.TryGetValue("token", out var token) || string.IsNullOrEmpty(token))
        {
            return Task.FromResult(new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: "Token is required in callback data",
                ProviderMetadata: null));
        }

        return VerifyPaymentTokenAsync(token, cancellationToken);
    }

    /// <summary>
    /// Verifies a payment token returned by ZainCash after user completes payment
    /// </summary>
    private Task<PaymentResult> VerifyPaymentTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);

            // Verify token signature
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.MerchantSecret));
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = handler.ValidateToken(token, validationParameters, out _);

            var status = jsonToken.Claims.FirstOrDefault(c => c.Type == "status")?.Value;
            var orderId = jsonToken.Claims.FirstOrDefault(c => c.Type == "orderid")?.Value;
            var transactionId = jsonToken.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            var amount = jsonToken.Claims.FirstOrDefault(c => c.Type == "amount")?.Value;

            if (status?.ToLower() == "success")
            {
                _logger.LogInformation("ZainCash payment verified successfully. Order: {OrderId}, Transaction: {TransactionId}",
                    orderId, transactionId);

                return Task.FromResult(new PaymentResult(
                    Success: true,
                    TransactionId: transactionId,
                    FailureReason: null,
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "Provider", "ZainCash" },
                        { "OrderId", orderId ?? string.Empty },
                        { "Amount", amount ?? string.Empty },
                        { "Status", status }
                    }));
            }
            else
            {
                _logger.LogWarning("ZainCash payment verification failed. Order: {OrderId}, Status: {Status}",
                    orderId, status);

                return Task.FromResult(new PaymentResult(
                    Success: false,
                    TransactionId: transactionId,
                    FailureReason: $"Payment status: {status}",
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "Status", status ?? "unknown" },
                        { "OrderId", orderId ?? string.Empty }
                    }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying ZainCash payment token");
            return Task.FromResult(new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Token verification failed: {ex.Message}",
                ProviderMetadata: null));
        }
    }

    /// <summary>
    /// Checks transaction status using ZainCash API
    /// </summary>
    public async Task<PaymentResult> CheckTransactionStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiBaseUrl = _config.IsTestMode 
                ? "https://test.zaincash.iq" 
                : "https://api.zaincash.iq";
            
            var statusEndpoint = $"{apiBaseUrl}/transaction/get";

            var formData = new List<KeyValuePair<string, string>>
            {
                new("id", transactionId),
                new("merchantId", _config.MerchantId)
            };

            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(statusEndpoint, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var statusResponse = JsonSerializer.Deserialize<ZainCashStatusResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (statusResponse?.Status?.ToLower() == "success")
                {
                    return new PaymentResult(
                        Success: true,
                        TransactionId: transactionId,
                        FailureReason: null,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Status", statusResponse.Status },
                            { "Message", statusResponse.Message ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: transactionId,
                FailureReason: "Transaction not found or failed",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking ZainCash transaction status for {TransactionId}", transactionId);
            return new PaymentResult(
                Success: false,
                TransactionId: transactionId,
                FailureReason: $"Status check failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    private string GenerateJwtToken(object data, string secret)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>();
        
        // Add claims from the data object using reflection
        // ZainCash expects lowercase claim names
        foreach (var prop in data.GetType().GetProperties())
        {
            var value = prop.GetValue(data);
            if (value != null)
            {
                var claimName = prop.Name.ToLower();
                var claimValue = value.ToString() ?? string.Empty;
                
                // Handle numeric values properly
                if (value is long || value is int || value is decimal || value is double)
                {
                    claims.Add(new Claim(claimName, claimValue, ClaimValueTypes.Integer64));
                }
                else
                {
                    claims.Add(new Claim(claimName, claimValue));
                }
            }
        }

        var token = new JwtSecurityToken(
            claims: claims,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<decimal> ConvertCurrencyAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        // Use ExchangeRateService if available (Multi-Currency Settlement #21)
        if (_exchangeRateService != null)
        {
            try
            {
                var convertedAmount = await _exchangeRateService.ConvertAsync(
                    amount,
                    fromCurrency,
                    toCurrency,
                    null,
                    cancellationToken);
                
                _logger.LogInformation(
                    "Currency conversion: {Amount} {FromCurrency} = {ConvertedAmount} {ToCurrency}",
                    amount, fromCurrency, convertedAmount, toCurrency);
                
                return convertedAmount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to convert currency from {FromCurrency} to {ToCurrency}. Using original amount.",
                    fromCurrency, toCurrency);
                return amount;
            }
        }

        // Fallback: no conversion service available
        _logger.LogWarning(
            "Currency conversion from {FromCurrency} to {ToCurrency} not available. ExchangeRateService not registered. Using original amount.",
            fromCurrency, toCurrency);
        return amount;
    }

    private class ZainCashInitResponse
    {
        public string? Status { get; set; }
        public string? Id { get; set; }
        public string? Message { get; set; }
    }

    private class ZainCashStatusResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
    }
}
