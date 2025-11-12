using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class TapPaymentProvider : IPaymentProvider
{
    private readonly ILogger<TapPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly TapProviderConfiguration _config;

    public string ProviderName => "Tap";

    public TapPaymentProvider(
        ILogger<TapPaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(TapPaymentProvider));
        _config = new TapProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Tap:BaseUrl"] ?? "https://api.tap.company/v2/",
            SecretKey = configuration["PaymentProviders:Tap:SecretKey"] ?? string.Empty,
            PublishableKey = configuration["PaymentProviders:Tap:PublishableKey"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Tap:IsTestMode", true)
        };

        // Set up authentication header
        // Tap uses Bearer token with secret key
        if (!string.IsNullOrEmpty(_config.SecretKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.SecretKey);
        }

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Tap Payments for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.SecretKey))
            {
                throw new InvalidOperationException("Tap Payments SecretKey must be configured");
            }

            // Convert amount to smallest currency unit (Tap uses smallest currency unit)
            var amountInSmallestUnit = ConvertToSmallestCurrencyUnit(request.Amount.Value, request.Currency.Code);

            // Tap requires source (payment method) from frontend
            // This should be provided in metadata
            if (request.Metadata == null || !request.Metadata.TryGetValue("source", out var source) || string.IsNullOrEmpty(source))
            {
                throw new InvalidOperationException("Tap Payments requires 'source' (payment method ID) in metadata. This should be obtained from Tap's JavaScript SDK.");
            }

            // Create charge request
            var chargeRequest = new TapChargeRequest
            {
                Amount = amountInSmallestUnit,
                Currency = request.Currency.Code,
                ThreeDSecure = true,
                SaveCard = false,
                Description = request.Metadata?.GetValueOrDefault("description") ?? $"Payment for order {request.OrderId}",
                StatementDescriptor = request.Metadata?.GetValueOrDefault("statement_descriptor") ?? "Payment",
                Metadata = new Dictionary<string, string>
                {
                    { "udf1", request.OrderId },
                    { "udf2", request.MerchantId }
                },
                Reference = new TapReference
                {
                    Transaction = request.OrderId,
                    Order = request.OrderId
                },
                Receipt = new TapReceipt
                {
                    Email = request.Metadata?.TryGetValue("customer_email", out var email) == true && !string.IsNullOrEmpty(email),
                    Sms = request.Metadata?.TryGetValue("customer_phone", out var phone) == true && !string.IsNullOrEmpty(phone)
                },
                Customer = new TapCustomer
                {
                    FirstName = request.Metadata?.GetValueOrDefault("customer_first_name") ?? string.Empty,
                    LastName = request.Metadata?.GetValueOrDefault("customer_last_name") ?? string.Empty,
                    Email = request.Metadata?.GetValueOrDefault("customer_email") ?? string.Empty,
                    Phone = new TapPhone
                    {
                        CountryCode = request.Metadata?.GetValueOrDefault("customer_phone_country_code") ?? "965",
                        Number = request.Metadata?.GetValueOrDefault("customer_phone") ?? string.Empty
                    }
                },
                Source = new TapSource
                {
                    Id = source
                },
                Redirect = new TapRedirect
                {
                    Url = request.Metadata?.GetValueOrDefault("return_url") ?? string.Empty
                }
            };

            // Handle split payments
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                // Tap supports split payments via destination
                if (request.Metadata?.TryGetValue("destination_account_id", out var destinationAccountId) == true)
                {
                    var ownerShareSmallestUnit = ConvertToSmallestCurrencyUnit(request.SplitPayment.OwnerShare, request.Currency.Code);
                    
                    chargeRequest.Destination = new TapDestination
                    {
                        Id = destinationAccountId,
                        Amount = ownerShareSmallestUnit
                    };

                    _logger.LogInformation("Tap split payment configured: Owner={OwnerShareSmallestUnit} smallest units to destination account",
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

            var apiUrl = $"{_config.BaseUrl}charges";
            _logger.LogInformation("Creating Tap charge for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var chargeResponse = JsonSerializer.Deserialize<TapChargeResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (chargeResponse != null && chargeResponse.Charge != null)
                {
                    var charge = chargeResponse.Charge;
                    var isSuccess = charge.Status == "CAPTURED" || charge.Status == "AUTHORIZED";
                    var requiresAction = charge.Status == "INITIATED" || charge.Status == "PENDING";

                    _logger.LogInformation("Tap charge created successfully. Charge ID: {ChargeId}, Status: {Status}",
                        charge.Id, charge.Status);

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: charge.Id ?? request.OrderId,
                        FailureReason: isSuccess ? null : $"Charge status: {charge.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "Tap" },
                            { "ChargeId", charge.Id ?? string.Empty },
                            { "Status", charge.Status ?? string.Empty },
                            { "RequiresAction", requiresAction.ToString() },
                            { "RedirectUrl", charge.Redirect?.Url ?? string.Empty },
                            { "Amount", charge.Amount.ToString() },
                            { "Currency", charge.Currency ?? string.Empty },
                            { "Reference", charge.Reference?.Transaction ?? string.Empty },
                            { "ProcessedAt", DateTime.UtcNow.ToString("O") },
                            { "IsTestMode", _config.IsTestMode.ToString() }
                        });
                }
            }

            // Handle errors
            var errorResponse = JsonSerializer.Deserialize<TapErrorResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var errorMessage = errorResponse?.Errors?.FirstOrDefault()?.Message ?? $"Tap Payments API error: {response.StatusCode}";
            _logger.LogError("Tap charge creation failed: {ErrorMessage}", errorMessage);

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
            _logger.LogError(ex, "Error processing Tap payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Tap payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Retrieves charge details
    /// </summary>
    public async Task<PaymentResult> GetChargeAsync(string chargeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiUrl = $"{_config.BaseUrl}charges/{chargeId}";
            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var chargeResponse = JsonSerializer.Deserialize<TapChargeResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (chargeResponse?.Charge != null)
                {
                    var charge = chargeResponse.Charge;
                    var isSuccess = charge.Status == "CAPTURED" || charge.Status == "AUTHORIZED";

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: charge.Id ?? chargeId,
                        FailureReason: isSuccess ? null : $"Charge status: {charge.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Status", charge.Status ?? string.Empty },
                            { "Amount", charge.Amount.ToString() },
                            { "Currency", charge.Currency ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: chargeId,
                FailureReason: "Failed to retrieve charge",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Tap charge {ChargeId}", chargeId);
            return new PaymentResult(
                Success: false,
                TransactionId: chargeId,
                FailureReason: $"Retrieval failed: {ex.Message}",
                ProviderMetadata: null);
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

    // Tap Payments API Models
    private class TapChargeRequest
    {
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public bool ThreeDSecure { get; set; } = true;
        public bool SaveCard { get; set; } = false;
        public string? Description { get; set; }
        public string? StatementDescriptor { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public TapReference? Reference { get; set; }
        public TapReceipt? Receipt { get; set; }
        public TapCustomer? Customer { get; set; }
        public TapSource Source { get; set; } = null!;
        public TapRedirect? Redirect { get; set; }
        public TapDestination? Destination { get; set; }
    }

    private class TapReference
    {
        public string Transaction { get; set; } = string.Empty;
        public string Order { get; set; } = string.Empty;
    }

    private class TapReceipt
    {
        public bool Email { get; set; }
        public bool Sms { get; set; }
    }

    private class TapCustomer
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public TapPhone? Phone { get; set; }
    }

    private class TapPhone
    {
        public string CountryCode { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
    }

    private class TapSource
    {
        public string Id { get; set; } = string.Empty;
    }

    private class TapRedirect
    {
        public string Url { get; set; } = string.Empty;
    }

    private class TapDestination
    {
        public string Id { get; set; } = string.Empty;
        public long Amount { get; set; }
    }

    private class TapChargeResponse
    {
        public TapCharge? Charge { get; set; }
    }

    private class TapCharge
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public long Amount { get; set; }
        public string? Currency { get; set; }
        public TapReference? Reference { get; set; }
        public TapRedirect? Redirect { get; set; }
    }

    private class TapErrorResponse
    {
        public List<TapError>? Errors { get; set; }
    }

    private class TapError
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }
}

