using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class AmazonPaymentProvider : IPaymentProvider
{
    private readonly ILogger<AmazonPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly AmazonProviderConfiguration _config;

    public string ProviderName => "AmazonPaymentServices";

    public AmazonPaymentProvider(
        ILogger<AmazonPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(AmazonPaymentProvider));
        _config = new AmazonProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Amazon:BaseUrl"] ?? "https://paymentservices.amazon.com/",
            AccessCode = configuration["PaymentProviders:Amazon:AccessCode"] ?? string.Empty,
            MerchantIdentifier = configuration["PaymentProviders:Amazon:MerchantIdentifier"] ?? string.Empty,
            ShaRequestPhrase = configuration["PaymentProviders:Amazon:ShaRequestPhrase"] ?? string.Empty,
            ShaResponsePhrase = configuration["PaymentProviders:Amazon:ShaResponsePhrase"] ?? string.Empty,
            ShaType = configuration["PaymentProviders:Amazon:ShaType"] ?? "SHA256",
            Language = configuration["PaymentProviders:Amazon:Language"] ?? "en",
            ReturnUrl = configuration["PaymentProviders:Amazon:ReturnUrl"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Amazon:IsTestMode", true)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Amazon Payment Services for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.AccessCode) || string.IsNullOrEmpty(_config.MerchantIdentifier))
            {
                throw new InvalidOperationException("Amazon Payment Services AccessCode and MerchantIdentifier must be configured");
            }

            if (string.IsNullOrEmpty(_config.ShaRequestPhrase))
            {
                throw new InvalidOperationException("Amazon Payment Services ShaRequestPhrase must be configured");
            }

            // Convert amount to smallest currency unit (e.g., AED 250.00 = 25000 fils)
            var amountInSmallestUnit = ConvertToSmallestCurrencyUnit(request.Amount.Value, request.Currency.Code);

            // Generate unique merchant reference
            var merchantReference = request.OrderId;

            // Build request parameters
            var requestParams = new Dictionary<string, string>
            {
                { "command", "PURCHASE" },
                { "access_code", _config.AccessCode },
                { "merchant_identifier", _config.MerchantIdentifier },
                { "merchant_reference", merchantReference },
                { "amount", amountInSmallestUnit.ToString() },
                { "currency", request.Currency.Code },
                { "language", _config.Language }
            };

            // Add optional parameters from metadata
            if (request.Metadata != null)
            {
                if (request.Metadata.TryGetValue("customer_email", out var customerEmail))
                {
                    requestParams["customer_email"] = customerEmail;
                }

                if (request.Metadata.TryGetValue("billing_address", out var billingAddress))
                {
                    requestParams["billing_address"] = billingAddress;
                }

                if (request.Metadata.TryGetValue("billing_city", out var billingCity))
                {
                    requestParams["billing_city"] = billingCity;
                }

                if (request.Metadata.TryGetValue("billing_country", out var billingCountry))
                {
                    requestParams["billing_country"] = billingCountry;
                }

                if (request.Metadata.TryGetValue("billing_postal_code", out var billingPostalCode))
                {
                    requestParams["billing_postal_code"] = billingPostalCode;
                }

                if (request.Metadata.TryGetValue("billing_state", out var billingState))
                {
                    requestParams["billing_state"] = billingState;
                }

                // Payment method token (if using tokenization)
                if (request.Metadata.TryGetValue("token_name", out var tokenName))
                {
                    requestParams["token_name"] = tokenName;
                }

                // Card details (for direct payment - requires PCI compliance)
                if (request.Metadata.TryGetValue("card_number", out var cardNumber))
                {
                    requestParams["card_number"] = cardNumber;
                }

                if (request.Metadata.TryGetValue("expiry_date", out var expiryDate))
                {
                    requestParams["expiry_date"] = expiryDate; // Format: YYYYMM
                }

                if (request.Metadata.TryGetValue("card_security_code", out var cardSecurityCode))
                {
                    requestParams["card_security_code"] = cardSecurityCode;
                }

                if (request.Metadata.TryGetValue("card_holder_name", out var cardHolderName))
                {
                    requestParams["card_holder_name"] = cardHolderName;
                }
            }

            // Add return URL if configured
            if (!string.IsNullOrEmpty(_config.ReturnUrl))
            {
                requestParams["return_url"] = _config.ReturnUrl;
            }

            // Handle split payments
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                // Amazon Payment Services doesn't natively support split payments
                // You would need to process two separate transactions or handle it at application level
                _logger.LogWarning("Amazon Payment Services doesn't natively support split payments. Processing as single payment.");
            }

            // Generate signature
            var signature = GenerateSignature(requestParams, _config.ShaRequestPhrase, _config.ShaType);
            requestParams["signature"] = signature;

            // Determine API endpoint based on integration method
            // For Custom Integration (direct payment), use the payment API endpoint
            var apiUrl = $"{_config.BaseUrl}FortAPI/paymentApi";

            // Build form data for POST request
            var formData = new List<KeyValuePair<string, string>>();
            foreach (var param in requestParams)
            {
                formData.Add(new KeyValuePair<string, string>(param.Key, param.Value));
            }

            var content = new FormUrlEncodedContent(formData);

            _logger.LogInformation("Sending payment request to Amazon Payment Services for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Parse response (Amazon Payment Services returns form-encoded or JSON)
                var responseParams = ParseResponse(responseString);

                // Validate response signature
                var responseSignature = responseParams.GetValueOrDefault("signature");
                var isValidSignature = ValidateSignature(responseParams, responseSignature, _config.ShaResponsePhrase, _config.ShaType);

                if (!isValidSignature)
                {
                    _logger.LogWarning("Invalid response signature from Amazon Payment Services");
                    return new PaymentResult(
                        Success: false,
                        TransactionId: null,
                        FailureReason: "Invalid response signature",
                        ProviderMetadata: null);
                }

                var status = responseParams.GetValueOrDefault("status");
                var responseCode = responseParams.GetValueOrDefault("response_code");
                var responseMessage = responseParams.GetValueOrDefault("response_message");
                var fortId = responseParams.GetValueOrDefault("fort_id");
                var paymentOption = responseParams.GetValueOrDefault("payment_option");

                var isSuccess = status == "02" || status == "14"; // 02 = Success, 14 = Success (3D Secure)

                _logger.LogInformation("Amazon Payment Services payment processed. Status: {Status}, Response Code: {ResponseCode}, Fort ID: {FortId}",
                    status, responseCode, fortId);

                // If 3D Secure is required, return the 3DS URL
                if (status == "20" && responseParams.TryGetValue("3ds_url", out var threeDsUrl))
                {
                    return new PaymentResult(
                        Success: false,
                        TransactionId: fortId,
                        FailureReason: "3D Secure authentication required",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "AmazonPaymentServices" },
                            { "FortId", fortId ?? string.Empty },
                            { "Status", status ?? string.Empty },
                            { "ResponseCode", responseCode ?? string.Empty },
                            { "ResponseMessage", responseMessage ?? string.Empty },
                            { "ThreeDsUrl", threeDsUrl },
                            { "Requires3DS", "true" },
                            { "IsTestMode", _config.IsTestMode.ToString() }
                        });
                }

                return new PaymentResult(
                    Success: isSuccess,
                    TransactionId: fortId ?? merchantReference,
                    FailureReason: isSuccess ? null : responseMessage ?? $"Payment status: {status}",
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "Provider", "AmazonPaymentServices" },
                        { "FortId", fortId ?? string.Empty },
                        { "Status", status ?? string.Empty },
                        { "ResponseCode", responseCode ?? string.Empty },
                        { "ResponseMessage", responseMessage ?? string.Empty },
                        { "PaymentOption", paymentOption ?? string.Empty },
                        { "MerchantReference", merchantReference },
                        { "Amount", amountInSmallestUnit.ToString() },
                        { "Currency", request.Currency.Code },
                        { "IsTestMode", _config.IsTestMode.ToString() }
                    });
            }

            // Handle errors
            _logger.LogError("Amazon Payment Services API returned error status {StatusCode}: {Response}",
                response.StatusCode, responseString);

            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Amazon Payment Services API error: {response.StatusCode}",
                ProviderMetadata: new Dictionary<string, string>
                {
                    { "StatusCode", response.StatusCode.ToString() },
                    { "Response", responseString }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Amazon Payment Services payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Amazon Payment Services payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Generates SHA signature for request parameters
    /// </summary>
    private string GenerateSignature(Dictionary<string, string> parameters, string phrase, string shaType)
    {
        // Sort parameters alphabetically and build signature string
        var sortedParams = parameters
            .Where(p => p.Key != "signature") // Exclude signature from calculation
            .OrderBy(p => p.Key)
            .Select(p => $"{p.Key}={p.Value}")
            .ToList();

        var signatureString = string.Join("&", sortedParams) + phrase;

        return ComputeHash(signatureString, shaType);
    }

    /// <summary>
    /// Validates response signature
    /// </summary>
    private bool ValidateSignature(Dictionary<string, string> responseParams, string? receivedSignature, string phrase, string shaType)
    {
        if (string.IsNullOrEmpty(receivedSignature))
        {
            return false;
        }

        // Sort parameters alphabetically and build signature string
        var sortedParams = responseParams
            .Where(p => p.Key != "signature") // Exclude signature from calculation
            .OrderBy(p => p.Key)
            .Select(p => $"{p.Key}={p.Value}")
            .ToList();

        var signatureString = string.Join("&", sortedParams) + phrase;
        var computedSignature = ComputeHash(signatureString, shaType);

        return string.Equals(computedSignature, receivedSignature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes hash using specified algorithm
    /// </summary>
    private string ComputeHash(string input, string shaType)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes;

        hashBytes = shaType.ToUpper() switch
        {
            "SHA256" => SHA256.HashData(inputBytes),
            "SHA512" => SHA512.HashData(inputBytes),
            _ => SHA256.HashData(inputBytes) // Default to SHA256
        };

        // Convert byte array to hex string
        var hexString = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            hexString.AppendFormat("{0:x2}", b);
        }

        return hexString.ToString();
    }

    /// <summary>
    /// Parses response string (can be form-encoded or JSON)
    /// </summary>
    private Dictionary<string, string> ParseResponse(string responseString)
    {
        var result = new Dictionary<string, string>();

        // Try to parse as JSON first
        try
        {
            var jsonDoc = JsonDocument.Parse(responseString);
            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.GetString() ?? string.Empty;
            }
            return result;
        }
        catch
        {
            // If not JSON, parse as form-encoded
            var pairs = responseString.Split('&');
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Converts amount to smallest currency unit
    /// </summary>
    private long ConvertToSmallestCurrencyUnit(decimal amount, string currency)
    {
        // Most currencies use 100 as multiplier (cents, fils, etc.)
        // Some currencies like JPY don't have subunits
        var currenciesWithoutSubunits = new[] { "JPY", "KRW", "VND" };
        
        if (currenciesWithoutSubunits.Contains(currency, StringComparer.OrdinalIgnoreCase))
        {
            return (long)amount;
        }

        return (long)(amount * 100);
    }

    /// <summary>
    /// Tokenizes a payment method for future use
    /// </summary>
    public async Task<string?> TokenizePaymentMethodAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestParams = new Dictionary<string, string>
            {
                { "command", "TOKENIZATION" },
                { "access_code", _config.AccessCode },
                { "merchant_identifier", _config.MerchantIdentifier },
                { "merchant_reference", Guid.NewGuid().ToString() },
                { "language", _config.Language }
            };

            // Add card details from metadata
            if (request.Metadata != null)
            {
                if (request.Metadata.TryGetValue("card_number", out var cardNumber))
                {
                    requestParams["card_number"] = cardNumber;
                }

                if (request.Metadata.TryGetValue("expiry_date", out var expiryDate))
                {
                    requestParams["expiry_date"] = expiryDate;
                }

                if (request.Metadata.TryGetValue("card_holder_name", out var cardHolderName))
                {
                    requestParams["card_holder_name"] = cardHolderName;
                }
            }

            var signature = GenerateSignature(requestParams, _config.ShaRequestPhrase, _config.ShaType);
            requestParams["signature"] = signature;

            var formData = requestParams.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)).ToList();
            var content = new FormUrlEncodedContent(formData);

            var apiUrl = $"{_config.BaseUrl}FortAPI/paymentApi";
            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseParams = ParseResponse(responseString);
                var isValidSignature = ValidateSignature(responseParams, responseParams.GetValueOrDefault("signature"), 
                    _config.ShaResponsePhrase, _config.ShaType);

                if (isValidSignature && responseParams.TryGetValue("token_name", out var tokenName))
                {
                    _logger.LogInformation("Payment method tokenized successfully. Token: {TokenName}", tokenName);
                    return tokenName;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tokenizing payment method");
            return null;
        }
    }
}

