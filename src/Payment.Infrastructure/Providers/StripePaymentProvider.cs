using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

/// <summary>
/// Stripe payment provider implementation with 3D Secure support.
/// Follows Single Responsibility Principle - handles Stripe-specific payment processing and 3DS.
/// Implements IThreeDSecurePaymentProvider for 3DS authentication support.
/// </summary>
public class StripePaymentProvider : IThreeDSecurePaymentProvider
{
    private readonly ILogger<StripePaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly StripeProviderConfiguration _config;

    public string ProviderName => "Stripe";

    public StripePaymentProvider(
        ILogger<StripePaymentProvider> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(StripePaymentProvider));
        _config = new StripeProviderConfiguration
        {
            BaseUrl = configuration["PaymentProviders:Stripe:BaseUrl"] ?? "https://api.stripe.com/v1/",
            ApiKey = configuration["PaymentProviders:Stripe:ApiKey"] ?? string.Empty,
            PublishableKey = configuration["PaymentProviders:Stripe:PublishableKey"] ?? string.Empty,
            IsTestMode = configuration.GetValue<bool>("PaymentProviders:Stripe:IsTestMode", true)
        };

        // Set up authentication header
        // Stripe uses the secret key directly in the Authorization header
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ApiKey);
        }

        // Set Stripe API version
        _httpClient.DefaultRequestHeaders.Add("Stripe-Version", "2024-06-20");
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with Stripe for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // Validate configuration
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                throw new InvalidOperationException("Stripe ApiKey must be configured");
            }

            // Convert amount to cents (Stripe uses smallest currency unit)
            var amountInCents = ConvertToSmallestCurrencyUnit(request.Amount.Value, request.Currency.Code);

            // Create Payment Intent
            var paymentIntentRequest = new StripeCreatePaymentIntentRequest
            {
                Amount = amountInCents,
                Currency = request.Currency.Code.ToLower(),
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", request.OrderId },
                    { "merchant_id", request.MerchantId }
                }
            };

            // Add description if available
            if (request.Metadata?.TryGetValue("description", out var description) == true)
            {
                paymentIntentRequest.Description = description;
            }

            // Handle split payments using Stripe Connect
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);

                // For split payments, we need to use Stripe Connect
                // This requires a connected account ID in metadata
                if (request.Metadata?.TryGetValue("connected_account_id", out var connectedAccountId) == true)
                {
                    var systemShareCents = ConvertToSmallestCurrencyUnit(request.SplitPayment.SystemShare, request.Currency.Code);
                    var ownerShareCents = ConvertToSmallestCurrencyUnit(request.SplitPayment.OwnerShare, request.Currency.Code);

                    // Create application fee (system share)
                    paymentIntentRequest.ApplicationFeeAmount = systemShareCents;
                    paymentIntentRequest.OnBehalfOf = connectedAccountId;
                    paymentIntentRequest.TransferData = new StripeTransferData
                    {
                        Destination = connectedAccountId
                    };

                    _logger.LogInformation("Stripe Connect split payment configured: System={SystemShareCents} cents, Owner={OwnerShareCents} cents",
                        systemShareCents, ownerShareCents);
                }
                else
                {
                    _logger.LogWarning("Split payment requested but connected_account_id not provided in metadata. Processing as single payment.");
                }
            }

            // Stripe API expects form-urlencoded data
            var formData = new List<KeyValuePair<string, string>>
            {
                new("amount", paymentIntentRequest.Amount.ToString()),
                new("currency", paymentIntentRequest.Currency)
            };

            if (!string.IsNullOrEmpty(paymentIntentRequest.Description))
            {
                formData.Add(new KeyValuePair<string, string>("description", paymentIntentRequest.Description));
            }

            if (paymentIntentRequest.Metadata != null)
            {
                foreach (var metadata in paymentIntentRequest.Metadata)
                {
                    formData.Add(new KeyValuePair<string, string>($"metadata[{metadata.Key}]", metadata.Value));
                }
            }

            if (paymentIntentRequest.ApplicationFeeAmount.HasValue)
            {
                formData.Add(new KeyValuePair<string, string>("application_fee_amount", paymentIntentRequest.ApplicationFeeAmount.Value.ToString()));
            }

            if (!string.IsNullOrEmpty(paymentIntentRequest.OnBehalfOf))
            {
                formData.Add(new KeyValuePair<string, string>("on_behalf_of", paymentIntentRequest.OnBehalfOf));
            }

            if (paymentIntentRequest.TransferData?.Destination != null)
            {
                formData.Add(new KeyValuePair<string, string>("transfer_data[destination]", paymentIntentRequest.TransferData.Destination));
            }

            if (paymentIntentRequest.AutomaticPaymentMethods == true)
            {
                formData.Add(new KeyValuePair<string, string>("automatic_payment_methods[enabled]", "true"));
            }

            var content = new FormUrlEncodedContent(formData);

            var apiUrl = $"{_config.BaseUrl}payment_intents";
            _logger.LogInformation("Creating Stripe Payment Intent for order {OrderId}", request.OrderId);

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentIntent = JsonSerializer.Deserialize<StripePaymentIntentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentIntent != null && !string.IsNullOrEmpty(paymentIntent.Id))
                {
                    _logger.LogInformation("Stripe Payment Intent created successfully. Payment Intent ID: {PaymentIntentId}, Status: {Status}",
                        paymentIntent.Id, paymentIntent.Status);

                    // Note: Payment Intent status can be:
                    // - "requires_payment_method": Needs payment method
                    // - "requires_confirmation": Needs confirmation
                    // - "requires_action": Requires customer action (3D Secure, etc.)
                    // - "processing": Processing
                    // - "requires_capture": Requires capture
                    // - "canceled": Canceled
                    // - "succeeded": Succeeded

                    var isSuccess = paymentIntent.Status == "succeeded";
                    var requiresAction = paymentIntent.Status == "requires_action" || paymentIntent.Status == "requires_payment_method";

                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentIntent.Id,
                        FailureReason: isSuccess ? null : $"Payment Intent status: {paymentIntent.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "Stripe" },
                            { "PaymentIntentId", paymentIntent.Id },
                            { "Status", paymentIntent.Status ?? string.Empty },
                            { "ClientSecret", paymentIntent.ClientSecret ?? string.Empty },
                            { "RequiresAction", requiresAction.ToString() },
                            { "Amount", paymentIntent.Amount.ToString() },
                            { "Currency", paymentIntent.Currency ?? string.Empty },
                            { "PublishableKey", _config.PublishableKey },
                            { "ProcessedAt", DateTime.UtcNow.ToString("O") },
                            { "IsTestMode", _config.IsTestMode.ToString() }
                        });
                }
            }

            // Handle Stripe errors
            var errorResponse = JsonSerializer.Deserialize<StripeErrorResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var errorMessage = errorResponse?.Error?.Message ?? $"Stripe API error: {response.StatusCode}";
            _logger.LogError("Stripe payment creation failed: {ErrorMessage}", errorMessage);

            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: errorMessage,
                ProviderMetadata: new Dictionary<string, string>
                {
                    { "StatusCode", response.StatusCode.ToString() },
                    { "ErrorType", errorResponse?.Error?.Type ?? string.Empty },
                    { "ErrorCode", errorResponse?.Error?.Code ?? string.Empty },
                    { "Response", responseString }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"Stripe payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Confirms a Stripe Payment Intent (after customer provides payment method)
    /// </summary>
    public async Task<PaymentResult> ConfirmPaymentIntentAsync(string paymentIntentId, string paymentMethodId, CancellationToken cancellationToken = default)
    {
        try
        {
            var confirmRequest = new StripeConfirmPaymentIntentRequest
            {
                PaymentMethod = paymentMethodId
            };

            var formData = new List<KeyValuePair<string, string>>
            {
                new("payment_method", confirmRequest.PaymentMethod)
            };

            if (!string.IsNullOrEmpty(confirmRequest.ReturnUrl))
            {
                formData.Add(new KeyValuePair<string, string>("return_url", confirmRequest.ReturnUrl));
            }

            var content = new FormUrlEncodedContent(formData);
            var apiUrl = $"{_config.BaseUrl}payment_intents/{paymentIntentId}/confirm";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentIntent = JsonSerializer.Deserialize<StripePaymentIntentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentIntent != null)
                {
                    var isSuccess = paymentIntent.Status == "succeeded";
                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentIntent.Id,
                        FailureReason: isSuccess ? null : $"Payment Intent status: {paymentIntent.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Status", paymentIntent.Status ?? string.Empty },
                            { "ChargeId", paymentIntent.LatestCharge ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: paymentIntentId,
                FailureReason: "Failed to confirm payment intent",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming Stripe Payment Intent {PaymentIntentId}", paymentIntentId);
            return new PaymentResult(
                Success: false,
                TransactionId: paymentIntentId,
                FailureReason: $"Confirmation failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Retrieves a Stripe Payment Intent status
    /// </summary>
    public async Task<PaymentResult> GetPaymentIntentStatusAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiUrl = $"{_config.BaseUrl}payment_intents/{paymentIntentId}";
            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentIntent = JsonSerializer.Deserialize<StripePaymentIntentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentIntent != null)
                {
                    var isSuccess = paymentIntent.Status == "succeeded";
                    return new PaymentResult(
                        Success: isSuccess,
                        TransactionId: paymentIntent.Id,
                        FailureReason: isSuccess ? null : $"Payment Intent status: {paymentIntent.Status}",
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Status", paymentIntent.Status ?? string.Empty },
                            { "Amount", paymentIntent.Amount.ToString() },
                            { "Currency", paymentIntent.Currency ?? string.Empty },
                            { "ChargeId", paymentIntent.LatestCharge ?? string.Empty }
                        });
                }
            }

            return new PaymentResult(
                Success: false,
                TransactionId: paymentIntentId,
                FailureReason: "Failed to retrieve payment intent status",
                ProviderMetadata: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Stripe Payment Intent status for {PaymentIntentId}", paymentIntentId);
            return new PaymentResult(
                Success: false,
                TransactionId: paymentIntentId,
                FailureReason: $"Status check failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }

    /// <summary>
    /// Cancels a Stripe Payment Intent
    /// </summary>
    public async Task<bool> CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cancelRequest = new StripeCancelPaymentIntentRequest
            {
                CancellationReason = "requested_by_customer"
            };

            var formData = new List<KeyValuePair<string, string>>();
            
            if (!string.IsNullOrEmpty(cancelRequest.CancellationReason))
            {
                formData.Add(new KeyValuePair<string, string>("cancellation_reason", cancelRequest.CancellationReason));
            }

            var content = new FormUrlEncodedContent(formData);
            var apiUrl = $"{_config.BaseUrl}payment_intents/{paymentIntentId}/cancel";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Stripe Payment Intent cancelled successfully. Payment Intent ID: {PaymentIntentId}", paymentIntentId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Stripe Payment Intent {PaymentIntentId}", paymentIntentId);
            return false;
        }
    }

    /// <summary>
    /// Initiates 3D Secure authentication for a payment.
    /// Creates a Payment Intent with 3DS enabled and returns challenge data if required.
    /// </summary>
    public async Task<ThreeDSecureChallenge?> InitiateThreeDSecureAsync(
        PaymentRequest request,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating 3D Secure for Stripe payment, order {OrderId}", request.OrderId);

        try
        {
            // Check if payment method is provided (required for 3DS)
            if (request.Metadata == null || !request.Metadata.TryGetValue("payment_method_id", out var paymentMethodId) || string.IsNullOrEmpty(paymentMethodId))
            {
                _logger.LogWarning("Payment method ID not provided in metadata for 3DS initiation");
                return null;
            }

            // Convert amount to cents
            var amountInCents = ConvertToSmallestCurrencyUnit(request.Amount.Value, request.Currency.Code);

            // Create Payment Intent with payment method attached
            var formData = new List<KeyValuePair<string, string>>
            {
                new("amount", amountInCents.ToString()),
                new("currency", request.Currency.Code.ToLower()),
                new("payment_method", paymentMethodId),
                new("confirmation_method", "manual"),
                new("confirm", "true"),
                new("return_url", returnUrl)
            };

            // Add metadata
            if (request.Metadata != null)
            {
                foreach (var metadata in request.Metadata)
                {
                    if (metadata.Key != "payment_method_id") // Already used
                    {
                        formData.Add(new KeyValuePair<string, string>($"metadata[{metadata.Key}]", metadata.Value));
                    }
                }
            }

            var content = new FormUrlEncodedContent(formData);
            var apiUrl = $"{_config.BaseUrl}payment_intents";

            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var paymentIntent = JsonSerializer.Deserialize<StripePaymentIntentResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (paymentIntent != null)
                {
                    // Check if 3DS is required (status will be "requires_action")
                    if (paymentIntent.Status == "requires_action" && paymentIntent.NextAction != null)
                    {
                        var acsUrl = paymentIntent.NextAction.RedirectToUrl?.Url ?? paymentIntent.NextAction.UseStripeSdk?.Url;
                        if (!string.IsNullOrEmpty(acsUrl))
                        {
                            // Generate merchant data (MD) - typically payment intent ID
                            var md = paymentIntent.Id ?? Guid.NewGuid().ToString("N");
                            
                            // Generate PAReq (Payment Authentication Request)
                            // In Stripe's case, this is handled by their SDK, but we'll create a token
                            var pareq = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                                JsonSerializer.Serialize(new
                                {
                                    payment_intent_id = paymentIntent.Id,
                                    client_secret = paymentIntent.ClientSecret,
                                    return_url = returnUrl
                                })));

                            _logger.LogInformation("3D Secure challenge generated for Stripe Payment Intent {PaymentIntentId}", paymentIntent.Id);

                            return new ThreeDSecureChallenge(
                                acsUrl: acsUrl,
                                pareq: pareq,
                                md: md,
                                termUrl: returnUrl,
                                version: "2.2.0");
                        }
                    }

                    // If 3DS is not required, return null
                    _logger.LogInformation("3D Secure not required for Stripe Payment Intent {PaymentIntentId}, status: {Status}", 
                        paymentIntent.Id, paymentIntent.Status);
                    return null;
                }
            }

            _logger.LogWarning("Failed to create Stripe Payment Intent for 3DS: {Response}", responseString);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating 3D Secure for Stripe payment");
            return null;
        }
    }

    /// <summary>
    /// Completes 3D Secure authentication for a payment.
    /// Confirms the Payment Intent after 3DS authentication is complete.
    /// </summary>
    public async Task<ThreeDSecurePaymentResult> CompleteThreeDSecureAsync(
        PaymentRequest request,
        string pareq,
        string ares,
        string md,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing 3D Secure for Stripe payment, order {OrderId}", request.OrderId);

        try
        {
            // Extract payment intent ID from MD (merchant data)
            if (string.IsNullOrEmpty(md))
            {
                throw new ArgumentException("Merchant data (MD) is required to complete 3DS", nameof(md));
            }

            // In Stripe's case, MD typically contains the Payment Intent ID
            var paymentIntentId = md;

            // Retrieve the Payment Intent to check its status
            var apiUrl = $"{_config.BaseUrl}payment_intents/{paymentIntentId}";
            var getResponse = await _httpClient.GetAsync(apiUrl, cancellationToken);
            var getResponseString = await getResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!getResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to retrieve Stripe Payment Intent {PaymentIntentId}: {Response}", 
                    paymentIntentId, getResponseString);
                
                return new ThreeDSecurePaymentResult(
                    ThreeDSecureResult: new ThreeDSecureResult(
                        authenticated: false,
                        failureReason: "Payment Intent not found"),
                    PaymentResult: new PaymentResult(
                        Success: false,
                        TransactionId: null,
                        FailureReason: "Payment Intent not found",
                        ProviderMetadata: null));
            }

            var paymentIntent = JsonSerializer.Deserialize<StripePaymentIntentResponse>(getResponseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (paymentIntent == null)
            {
                throw new InvalidOperationException("Failed to deserialize Payment Intent response");
            }

            // If Payment Intent is already succeeded, 3DS was successful
            if (paymentIntent.Status == "succeeded")
            {
                _logger.LogInformation("Stripe Payment Intent {PaymentIntentId} already succeeded", paymentIntentId);

                // Extract 3DS data from Payment Intent metadata or charge
                var threeDSResult = new ThreeDSecureResult(
                    authenticated: true,
                    cavv: paymentIntent.Metadata?.GetValueOrDefault("3ds_cavv"),
                    eci: paymentIntent.Metadata?.GetValueOrDefault("3ds_eci"),
                    xid: paymentIntent.Metadata?.GetValueOrDefault("3ds_xid"),
                    version: "2.2.0");

                return new ThreeDSecurePaymentResult(
                    ThreeDSecureResult: threeDSResult,
                    PaymentResult: new PaymentResult(
                        Success: true,
                        TransactionId: paymentIntent.Id,
                        FailureReason: null,
                        ProviderMetadata: new Dictionary<string, string>
                        {
                            { "Provider", "Stripe" },
                            { "PaymentIntentId", paymentIntent.Id ?? string.Empty },
                            { "Status", paymentIntent.Status ?? string.Empty },
                            { "ChargeId", paymentIntent.LatestCharge ?? string.Empty }
                        }));
            }

            // If Payment Intent requires action, try to confirm it
            if (paymentIntent.Status == "requires_action" || paymentIntent.Status == "requires_confirmation")
            {
                // Confirm the Payment Intent (Stripe will handle the 3DS completion)
                var confirmFormData = new List<KeyValuePair<string, string>>();
                
                // If return_url is needed
                if (request.Metadata?.TryGetValue("return_url", out var returnUrl) == true && !string.IsNullOrEmpty(returnUrl))
                {
                    confirmFormData.Add(new KeyValuePair<string, string>("return_url", returnUrl));
                }

                var confirmContent = confirmFormData.Count > 0 
                    ? new FormUrlEncodedContent(confirmFormData) 
                    : new FormUrlEncodedContent(new List<KeyValuePair<string, string>>());

                var confirmUrl = $"{_config.BaseUrl}payment_intents/{paymentIntentId}/confirm";
                var confirmResponse = await _httpClient.PostAsync(confirmUrl, confirmContent, cancellationToken);
                var confirmResponseString = await confirmResponse.Content.ReadAsStringAsync(cancellationToken);

                if (confirmResponse.IsSuccessStatusCode)
                {
                    var confirmedIntent = JsonSerializer.Deserialize<StripePaymentIntentResponse>(confirmResponseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (confirmedIntent != null && confirmedIntent.Status == "succeeded")
                    {
                        var threeDSResult = new ThreeDSecureResult(
                            authenticated: true,
                            cavv: confirmedIntent.Metadata?.GetValueOrDefault("3ds_cavv"),
                            eci: confirmedIntent.Metadata?.GetValueOrDefault("3ds_eci"),
                            xid: confirmedIntent.Metadata?.GetValueOrDefault("3ds_xid"),
                            version: "2.2.0");

                        return new ThreeDSecurePaymentResult(
                            ThreeDSecureResult: threeDSResult,
                            PaymentResult: new PaymentResult(
                                Success: true,
                                TransactionId: confirmedIntent.Id,
                                FailureReason: null,
                                ProviderMetadata: new Dictionary<string, string>
                                {
                                    { "Provider", "Stripe" },
                                    { "PaymentIntentId", confirmedIntent.Id ?? string.Empty },
                                    { "Status", confirmedIntent.Status ?? string.Empty },
                                    { "ChargeId", confirmedIntent.LatestCharge ?? string.Empty }
                                }));
                    }
                }
            }

            // If we get here, 3DS failed or payment intent is in an unexpected state
            _logger.LogWarning("Stripe Payment Intent {PaymentIntentId} in unexpected state: {Status}", 
                paymentIntentId, paymentIntent.Status);

            return new ThreeDSecurePaymentResult(
                ThreeDSecureResult: new ThreeDSecureResult(
                    authenticated: false,
                    failureReason: $"Payment Intent status: {paymentIntent.Status}"),
                PaymentResult: new PaymentResult(
                    Success: false,
                    TransactionId: paymentIntentId,
                    FailureReason: $"Payment Intent status: {paymentIntent.Status}",
                    ProviderMetadata: new Dictionary<string, string>
                    {
                        { "Status", paymentIntent.Status ?? string.Empty }
                    }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing 3D Secure for Stripe payment");
            return new ThreeDSecurePaymentResult(
                ThreeDSecureResult: new ThreeDSecureResult(
                    authenticated: false,
                    failureReason: $"Error: {ex.Message}"),
                PaymentResult: new PaymentResult(
                    Success: false,
                    TransactionId: null,
                    FailureReason: $"3DS completion failed: {ex.Message}",
                    ProviderMetadata: null));
        }
    }

    /// <summary>
    /// Checks if 3D Secure authentication is required for a payment.
    /// Stripe automatically determines if 3DS is required based on card, amount, and region.
    /// </summary>
    public async Task<bool> IsThreeDSecureRequiredAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Stripe automatically determines if 3DS is required based on:
        // - Card issuer requirements
        // - Payment amount
        // - Regional regulations (e.g., SCA in Europe)
        // - Risk assessment

        // For now, we'll check if payment method is provided and amount is above threshold
        // In production, you might want to create a test Payment Intent to check if 3DS is required

        if (request.Metadata == null || !request.Metadata.TryGetValue("payment_method_id", out var paymentMethodId) || string.IsNullOrEmpty(paymentMethodId))
        {
            return false;
        }

        // Check amount threshold (3DS typically required for amounts above certain thresholds)
        // European regulations require 3DS for payments above €0 (SCA)
        var amountThreshold = request.Currency.Code == "EUR" ? 0m : 100m;
        
        if (request.Amount.Value >= amountThreshold)
        {
            _logger.LogDebug("3DS may be required for Stripe payment: amount {Amount} {Currency} is above threshold {Threshold}",
                request.Amount.Value, request.Currency.Code, amountThreshold);
            return true;
        }

        return false;
    }

    private long ConvertToSmallestCurrencyUnit(decimal amount, string currency)
    {
        // Stripe uses cents for most currencies, but some use different multipliers
        // For simplicity, we'll use cents for all currencies
        // In production, you should handle currency-specific multipliers
        return (long)(amount * 100);
    }

    // Stripe API Models
    private class StripeCreatePaymentIntentRequest
    {
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public long? ApplicationFeeAmount { get; set; }
        public string? OnBehalfOf { get; set; }
        public StripeTransferData? TransferData { get; set; }
        public bool? AutomaticPaymentMethods { get; set; } = true;
    }

    private class StripeTransferData
    {
        public string? Destination { get; set; }
    }

    private class StripeConfirmPaymentIntentRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
    }

    private class StripeCancelPaymentIntentRequest
    {
        public string? CancellationReason { get; set; }
    }

    private class StripePaymentIntentResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public long Amount { get; set; }
        public string? Currency { get; set; }
        public string? ClientSecret { get; set; }
        public string? LatestCharge { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public StripeNextAction? NextAction { get; set; }
    }

    private class StripeNextAction
    {
        public string? Type { get; set; }
        public StripeRedirectToUrl? RedirectToUrl { get; set; }
        public StripeUseStripeSdk? UseStripeSdk { get; set; }
    }

    private class StripeRedirectToUrl
    {
        public string? Url { get; set; }
        public string? ReturnUrl { get; set; }
    }

    private class StripeUseStripeSdk
    {
        public string? Type { get; set; }
        public string? Url { get; set; }
    }

    private class StripeErrorResponse
    {
        public StripeError? Error { get; set; }
    }

    private class StripeError
    {
        public string? Type { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }
    }
}
