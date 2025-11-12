using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Implementation of 3D Secure authentication service.
/// Follows Single Responsibility Principle - only handles 3DS operations.
/// Stateless by design for Kubernetes deployment.
/// </summary>
public class ThreeDSecureService : IThreeDSecureService
{
    private readonly ILogger<ThreeDSecureService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;

    public ThreeDSecureService(
        ILogger<ThreeDSecureService> logger,
        IConfiguration configuration,
        IUnitOfWork unitOfWork)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ThreeDSecureChallenge?> InitiateAuthenticationAsync(
        Guid paymentId,
        Amount amount,
        Currency currency,
        CardToken cardToken,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Initiating 3D Secure authentication for payment {PaymentId}, amount {Amount} {Currency}",
            paymentId, amount.Value, currency.Code);

        // Get payment to determine provider
        var payment = await _unitOfWork.Payments.GetByIdAsync(
            paymentId,
            cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found for 3DS initiation", paymentId);
            throw new InvalidOperationException($"Payment {paymentId} not found");
        }

        // Check if 3DS is enabled for this provider
        var threeDSEnabled = _configuration.GetValue<bool>($"PaymentProviders:{payment.Provider.Name}:ThreeDSecure:Enabled", false);
        if (!threeDSEnabled)
        {
            _logger.LogInformation("3D Secure is not enabled for provider {Provider}", payment.Provider.Name);
            return null;
        }

        // Generate 3DS challenge data
        // In a real implementation, this would call the payment provider's 3DS API
        // For now, we'll create a mock challenge that follows 3DS 2.2.0 protocol
        
        var baseUrl = _configuration["ThreeDSecure:BaseUrl"] ?? _configuration["PaymentProviders:BaseUrl"] ?? "https://api.payment.com";
        var termUrl = $"{baseUrl}/api/v1/payments/{paymentId}/3ds/callback";
        
        // Generate unique identifiers for the 3DS flow
        var md = Guid.NewGuid().ToString("N"); // Merchant data
        var pareq = GeneratePareq(paymentId, amount, currency, cardToken, md);
        var acsUrl = _configuration[$"PaymentProviders:{payment.Provider.Name}:ThreeDSecure:AcsUrl"] 
                     ?? _configuration["ThreeDSecure:DefaultAcsUrl"] 
                     ?? "https://acs.example.com/authenticate";

        _logger.LogInformation(
            "Generated 3DS challenge for payment {PaymentId}. ACS URL: {AcsUrl}",
            paymentId, acsUrl);

        return new ThreeDSecureChallenge(
            acsUrl,
            pareq,
            md,
            termUrl,
            "2.2.0");
    }

    public async Task<ThreeDSecureResult> CompleteAuthenticationAsync(
        Guid paymentId,
        string pareq,
        string ares,
        string md,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Completing 3D Secure authentication for payment {PaymentId}", paymentId);

        // Get payment to determine provider
        var payment = await _unitOfWork.Payments.GetByIdAsync(
            paymentId,
            cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found for 3DS completion", paymentId);
            throw new InvalidOperationException($"Payment {paymentId} not found");
        }

        // Validate MD matches what we sent
        var expectedMd = payment.Metadata.GetValueOrDefault("3ds_md");
        if (expectedMd != md)
        {
            _logger.LogWarning(
                "3DS MD mismatch for payment {PaymentId}. Expected: {ExpectedMd}, Received: {ReceivedMd}",
                paymentId, expectedMd, md);
            return new ThreeDSecureResult(
                authenticated: false,
                failureReason: "Merchant data mismatch");
        }

        // Parse ARes (Authentication Response) from ACS
        // In a real implementation, this would parse the actual ARes XML/JSON from the ACS
        // For now, we'll simulate parsing and validation
        
        var authenticated = ValidateAres(ares);
        
        if (authenticated)
        {
            // Extract CAVV, ECI, XID from ARes
            // In a real implementation, these would be parsed from the ARes response
            var cavv = ExtractCavvFromAres(ares);
            var eci = ExtractEciFromAres(ares);
            var xid = ExtractXidFromAres(ares);

            _logger.LogInformation(
                "3D Secure authentication successful for payment {PaymentId}. CAVV: {Cavv}, ECI: {Eci}",
                paymentId, cavv, eci);

            return new ThreeDSecureResult(
                authenticated: true,
                cavv: cavv,
                eci: eci,
                xid: xid,
                version: "2.2.0",
                ares: ares);
        }
        else
        {
            _logger.LogWarning("3D Secure authentication failed for payment {PaymentId}", paymentId);
            return new ThreeDSecureResult(
                authenticated: false,
                failureReason: "Authentication failed");
        }
    }

    public Task<bool> IsAuthenticationRequiredAsync(
        Amount amount,
        Currency currency,
        CardToken cardToken,
        CancellationToken cancellationToken = default)
    {
        // Check if 3DS is globally enabled
        var threeDSEnabled = _configuration.GetValue<bool>("ThreeDSecure:Enabled", true);
        if (!threeDSEnabled)
        {
            return Task.FromResult(false);
        }

        // Check amount threshold (3DS may be required for amounts above a threshold)
        var amountThreshold = _configuration.GetValue<decimal>("ThreeDSecure:AmountThreshold", 0);
        if (amountThreshold > 0 && amount.Value < amountThreshold)
        {
            _logger.LogDebug(
                "3DS not required: amount {Amount} is below threshold {Threshold}",
                amount.Value, amountThreshold);
            return Task.FromResult(false);
        }

        // Check currency-specific rules
        var requiredCurrencies = _configuration.GetSection("ThreeDSecure:RequiredCurrencies").Get<string[]>();
        if (requiredCurrencies != null && requiredCurrencies.Length > 0)
        {
            var isRequiredCurrency = requiredCurrencies.Contains(currency.Code, StringComparer.OrdinalIgnoreCase);
            if (!isRequiredCurrency)
            {
                _logger.LogDebug(
                    "3DS not required: currency {Currency} is not in required list",
                    currency.Code);
                return Task.FromResult(false);
            }
        }

        // Check card brand rules (some card brands may require 3DS)
        var requiredCardBrands = _configuration.GetSection("ThreeDSecure:RequiredCardBrands").Get<string[]>();
        if (requiredCardBrands != null && requiredCardBrands.Length > 0)
        {
            var isRequiredBrand = requiredCardBrands.Contains(cardToken.CardBrand, StringComparer.OrdinalIgnoreCase);
            if (!isRequiredBrand)
            {
                _logger.LogDebug(
                    "3DS not required: card brand {CardBrand} is not in required list",
                    cardToken.CardBrand);
                return Task.FromResult(false);
            }
        }

        // Default: 3DS is required for card payments above threshold
        return Task.FromResult(true);
    }

    /// <summary>
    /// Generates a Payment Authentication Request (PAReq) for 3DS.
    /// In a real implementation, this would be generated according to 3DS protocol specifications.
    /// </summary>
    private string GeneratePareq(
        Guid paymentId,
        Amount amount,
        Currency currency,
        CardToken cardToken,
        string md)
    {
        // In a real implementation, this would generate a proper PAReq XML/JSON
        // following the 3DS 2.2.0 protocol specification
        // For now, we'll create a base64-encoded JSON structure
        
        var pareqData = new
        {
            messageVersion = "2.2.0",
            messageType = "PAReq",
            paymentId = paymentId.ToString(),
            amount = amount.Value,
            currency = currency.Code,
            cardToken = cardToken.Token,
            md = md,
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = System.Text.Json.JsonSerializer.Serialize(pareqData);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Validates the Authentication Response (ARes) from the ACS.
    /// In a real implementation, this would parse and validate the actual ARes XML/JSON.
    /// </summary>
    private bool ValidateAres(string ares)
    {
        if (string.IsNullOrWhiteSpace(ares))
        {
            return false;
        }

        try
        {
            // In a real implementation, this would:
            // 1. Parse the ARes XML/JSON
            // 2. Validate the signature
            // 3. Check the authentication status
            // 4. Verify the transaction identifier matches
            
            // For now, we'll do a simple check
            // In production, this should be a proper XML/JSON parser with signature validation
            return ares.Contains("authenticated", StringComparison.OrdinalIgnoreCase) ||
                   ares.Contains("Y", StringComparison.OrdinalIgnoreCase); // "Y" indicates authenticated
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ARes");
            return false;
        }
    }

    /// <summary>
    /// Extracts CAVV from ARes.
    /// In a real implementation, this would parse the actual ARes XML/JSON.
    /// </summary>
    private string? ExtractCavvFromAres(string ares)
    {
        // In a real implementation, this would parse the ARes and extract CAVV
        // For now, we'll generate a mock CAVV
        return Guid.NewGuid().ToString("N").Substring(0, 28); // CAVV is typically 28 characters
    }

    /// <summary>
    /// Extracts ECI from ARes.
    /// In a real implementation, this would parse the actual ARes XML/JSON.
    /// </summary>
    private string? ExtractEciFromAres(string ares)
    {
        // In a real implementation, this would parse the ARes and extract ECI
        // ECI values: "05" (3DS authenticated), "06" (3DS attempted), "07" (3DS not authenticated)
        return "05"; // Mock ECI indicating 3DS authenticated
    }

    /// <summary>
    /// Extracts XID from ARes.
    /// In a real implementation, this would parse the actual ARes XML/JSON.
    /// </summary>
    private string? ExtractXidFromAres(string ares)
    {
        // In a real implementation, this would parse the ARes and extract XID
        // For now, we'll generate a mock XID
        return Guid.NewGuid().ToString("N").Substring(0, 20); // XID is typically 20 characters
    }
}

