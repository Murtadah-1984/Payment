using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Metrics;

namespace Payment.Infrastructure.Providers;

/// <summary>
/// Payment provider for Tap-to-Pay transactions using NFC/HCE tokens.
/// Supports Apple Pay, Google Pay, and Tap Company SDK tokenized payments.
/// Implements security best practices: token validation, replay prevention, and PCI-DSS compliance.
/// </summary>
public class TapToPayPaymentProvider : IPaymentProvider
{
    private readonly ILogger<TapToPayPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly TapToPayProviderConfiguration _config;
    private readonly ICacheService _cacheService;
    private const string ReplayPreventionCacheKeyPrefix = "tap_to_pay_token:";
    private const int ReplayPreventionTtlHours = 24; // Tokens expire after 24 hours

    public string ProviderName => "TapToPay";

    public TapToPayPaymentProvider(
        ILogger<TapToPayPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICacheService cacheService)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(TapToPayPaymentProvider));
        _cacheService = cacheService;
        _config = new TapToPayProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:TapToPay:BaseUrl"] ?? "https://api.tap.company/v2/",
            SecretKey = configuration["PaymentProviders:TapToPay:SecretKey"] ?? string.Empty,
            PublishableKey = configuration["PaymentProviders:TapToPay:PublishableKey"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:TapToPay:IsTestMode", true),
            ReplayPreventionEnabled = configuration.GetValue<bool>("PaymentProviders:TapToPay:ReplayPreventionEnabled", true)
        };

        // Set up authentication header
        if (!string.IsNullOrEmpty(_config.SecretKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.SecretKey);
        }

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Processing Tap-to-Pay payment for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.SecretKey))
            {
                throw new InvalidOperationException("TapToPay SecretKey must be configured");
            }

            // Extract NFC token from metadata (required for Tap-to-Pay)
            if (request.Metadata == null || !request.Metadata.TryGetValue("nfc_token", out var nfcToken) || string.IsNullOrEmpty(nfcToken))
            {
                _logger.LogError("NFC token is required for Tap-to-Pay transactions. OrderId: {OrderId}", request.OrderId);
                return new PaymentResult(
                    Success: false,
                    TransactionId: null,
                    FailureReason: "NFC token is required for Tap-to-Pay transactions",
                    ProviderMetadata: null);
            }

            // Replay prevention: Check if token has already been processed using distributed cache
            if (_config.ReplayPreventionEnabled)
            {
                var tokenHash = ComputeTokenHash(nfcToken);
                var cacheKey = $"{ReplayPreventionCacheKeyPrefix}{tokenHash}";
                
                // Check if token was already processed (using distributed cache for stateless microservice)
                // Use a simple marker class to work with ICacheService which requires a class type
                var existingToken = await _cacheService.GetAsync<TokenMarker>(cacheKey, cancellationToken);
                if (existingToken != null)
                {
                    _logger.LogWarning("Duplicate NFC token detected. Possible replay attack. OrderId: {OrderId}, TokenHash: {TokenHash}",
                        request.OrderId, tokenHash);
                    
                    // Record replay attempt metric
                    TapToPayMetrics.RecordReplayAttempt();
                    
                    return new PaymentResult(
                        Success: false,
                        TransactionId: null,
                        FailureReason: "NFC token has already been processed. Possible replay attack.",
                        ProviderMetadata: new Dictionary<string, string> { { "ReplayDetected", "true" } });
                }

                // Mark token as processed in distributed cache with TTL
                await _cacheService.SetAsync(cacheKey, new TokenMarker { Hash = tokenHash }, TimeSpan.FromHours(ReplayPreventionTtlHours), cancellationToken);
                _logger.LogDebug("NFC token hash cached for replay prevention. TokenHash: {TokenHash}, TTL: {TtlHours} hours",
                    tokenHash, ReplayPreventionTtlHours);
            }

            // Extract device ID and customer ID from metadata (optional)
            request.Metadata.TryGetValue("device_id", out var deviceId);
            request.Metadata.TryGetValue("customer_id", out var customerId);

            // Convert amount to smallest currency unit (Tap uses smallest currency unit)
            var amountInSmallestUnit = ConvertToSmallestCurrencyUnit(request.Amount.Value, request.Currency.Code);

            // Create Tap-to-Pay charge request
            var chargeRequest = new TapToPayChargeRequest
            {
                Amount = amountInSmallestUnit,
                Currency = request.Currency.Code,
                ThreeDSecure = true,
                SaveCard = false,
                Description = request.Metadata?.GetValueOrDefault("description") ?? $"Tap-to-Pay payment for order {request.OrderId}",
                StatementDescriptor = request.Metadata?.GetValueOrDefault("statement_descriptor") ?? "Tap-to-Pay Payment",
                Metadata = new Dictionary<string, string>
                {
                    { "udf1", request.OrderId },
                    { "udf2", request.MerchantId },
                    { "payment_method", "tap_to_pay" },
                    { "device_id", deviceId ?? string.Empty },
                    { "customer_id", customerId ?? string.Empty }
                },
                Reference = new TapToPayReference
                {
                    Transaction = request.OrderId,
                    Order = request.OrderId
                },
                Receipt = new TapToPayReceipt
                {
                    Email = request.Metadata?.TryGetValue("customer_email", out var email) == true && !string.IsNullOrEmpty(email),
                    Sms = request.Metadata?.TryGetValue("customer_phone", out var phone) == true && !string.IsNullOrEmpty(phone)
                },
                Customer = new TapToPayCustomer
                {
                    FirstName = request.Metadata?.GetValueOrDefault("customer_first_name") ?? string.Empty,
                    LastName = request.Metadata?.GetValueOrDefault("customer_last_name") ?? string.Empty,
                    Email = request.Metadata?.GetValueOrDefault("customer_email") ?? string.Empty,
                    Phone = new TapToPayPhone
                    {
                        CountryCode = request.Metadata?.GetValueOrDefault("customer_phone_country_code") ?? "965",
                        Number = request.Metadata?.GetValueOrDefault("customer_phone") ?? string.Empty
                    }
                },
                Source = new TapToPaySource
                {
                    Id = nfcToken // NFC token from mobile SDK
                },
                Redirect = new TapToPayRedirect
                {
                    Url = request.Metadata?.GetValueOrDefault("return_url") ?? string.Empty
                }
            };

            // Handle split payments
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                if (request.Metadata?.TryGetValue("destination_account_id", out var destinationAccountId) == true)
                {
                    var ownerShareSmallestUnit = ConvertToSmallestCurrencyUnit(request.SplitPayment.OwnerShare, request.Currency.Code);
                    
                    chargeRequest.Destination = new TapToPayDestination
                    {
                        Id = destinationAccountId,
                        Amount = ownerShareSmallestUnit
                    };

                    _logger.LogInformation("Tap-to-Pay split payment configured: Owner={OwnerShareSmallestUnit} smallest units to destination account",
                        ownerShareSmallestUnit);
                }
                else
                {
                    _logger.LogWarning("Split payment requested but destination_account_id not provided. Processing as single payment.");
                }
            }

            var jsonContent = JsonSerializer.Serialize(chargeRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Use Tap Payments API endpoint for NFC payments
            var apiUrl = $"{_config.BaseUrl}charges";
            _logger.LogInformation("Creating Tap-to-Pay charge for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var chargeResponse = JsonSerializer.Deserialize<TapToPayChargeResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (chargeResponse != null && chargeResponse.Charge != null)
                {
                    var charge = chargeResponse.Charge;
                    var isSuccess = charge.Status == "CAPTURED" || charge.Status == "AUTHORIZED";
                    var requiresAction = charge.Status == "INITIATED" || charge.Status == "PENDING";

                    _logger.LogInformation("Tap-to-Pay charge created successfully. Charge ID: {ChargeId}, Status: {Status}",
                        charge.Id, charge.Status);

                    // Record success metrics
                    stopwatch.Stop();
                    if (isSuccess)
                    {
                        TapToPayMetrics.RecordSuccess(stopwatch.Elapsed.TotalSeconds);
                    }
                    else
                    {
                        TapToPayMetrics.RecordFailure("pending_status", stopwatch.Elapsed.TotalSeconds);
                    }

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: charge.Id ?? request.OrderId,
                        FailureReason: isSuccess ? null : $"Charge status: {charge.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "TapToPay" },
                            { "ChargeId", charge.Id ?? string.Empty },
                            { "Status", charge.Status ?? string.Empty },
                            { "RequiresAction", requiresAction.ToString() },
                            { "RedirectUrl", charge.Redirect?.Url ?? string.Empty },
                            { "Amount", charge.Amount.ToString() },
                            { "Currency", charge.Currency ?? string.Empty },
                            { "Reference", charge.Reference?.Transaction ?? string.Empty },
                            { "ProcessedAt", DateTime.UtcNow.ToString("O") },
                            { "IsTestMode", _config.IsTestMode.ToString() },
                            { "PaymentMethod", "TapToPay" },
                            { "DeviceId", deviceId ?? string.Empty }
                        });
                }
            }

            // Handle errors
            var errorResponse = JsonSerializer.Deserialize<TapToPayErrorResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var errorMessage = errorResponse?.Errors?.FirstOrDefault()?.Message ?? $"Tap-to-Pay API error: {response.StatusCode}";
            _logger.LogError("Tap-to-Pay charge creation failed: {ErrorMessage}", errorMessage);

            // Record failure metrics
            stopwatch.Stop();
            var errorType = errorResponse?.Errors?.FirstOrDefault()?.Code ?? response.StatusCode.ToString();
            TapToPayMetrics.RecordFailure(errorType, stopwatch.Elapsed.TotalSeconds);

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
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing Tap-to-Pay payment for order {OrderId}", request.OrderId);
            
            // Record failure metrics
            TapToPayMetrics.RecordFailure(ex.GetType().Name, stopwatch.Elapsed.TotalSeconds);
            
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Tap-to-Pay payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Computes a SHA-256 hash of the NFC token for replay prevention.
    /// In production, this should be stored in a distributed cache (Redis) with TTL.
    /// </summary>
    private static string ComputeTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
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

    // Tap-to-Pay API Models
    private class TapToPayChargeRequest
    {
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public bool ThreeDSecure { get; set; } = true;
        public bool SaveCard { get; set; } = false;
        public string? Description { get; set; }
        public string? StatementDescriptor { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public TapToPayReference? Reference { get; set; }
        public TapToPayReceipt? Receipt { get; set; }
        public TapToPayCustomer? Customer { get; set; }
        public TapToPaySource Source { get; set; } = null!;
        public TapToPayRedirect? Redirect { get; set; }
        public TapToPayDestination? Destination { get; set; }
    }

    private class TapToPayReference
    {
        public string Transaction { get; set; } = string.Empty;
        public string Order { get; set; } = string.Empty;
    }

    private class TapToPayReceipt
    {
        public bool Email { get; set; }
        public bool Sms { get; set; }
    }

    private class TapToPayCustomer
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public TapToPayPhone? Phone { get; set; }
    }

    private class TapToPayPhone
    {
        public string CountryCode { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
    }

    private class TapToPaySource
    {
        public string Id { get; set; } = string.Empty;
    }

    private class TapToPayRedirect
    {
        public string Url { get; set; } = string.Empty;
    }

    private class TapToPayDestination
    {
        public string Id { get; set; } = string.Empty;
        public long Amount { get; set; }
    }

    private class TapToPayChargeResponse
    {
        public TapToPayCharge? Charge { get; set; }
    }

    private class TapToPayCharge
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public long Amount { get; set; }
        public string? Currency { get; set; }
        public TapToPayReference? Reference { get; set; }
        public TapToPayRedirect? Redirect { get; set; }
    }

    private class TapToPayErrorResponse
    {
        public List<TapToPayError>? Errors { get; set; }
    }

    private class TapToPayError
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// Marker class for storing token hash in distributed cache.
    /// Required because ICacheService requires a class type.
    /// </summary>
    private class TokenMarker
    {
        public string Hash { get; set; } = string.Empty;
    }
}

