namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for fraud detection service.
/// Follows Interface Segregation Principle - focused on fraud detection only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface IFraudDetectionService
{
    /// <summary>
    /// Checks if a payment request is potentially fraudulent.
    /// </summary>
    /// <param name="request">The fraud check request containing payment details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Fraud check result indicating risk level and recommendation</returns>
    Task<FraudCheckResult> CheckAsync(FraudCheckRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request object for fraud detection check.
/// Immutable record following DDD value object pattern.
/// </summary>
public sealed record FraudCheckRequest(
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CustomerId,
    string? DeviceId,
    string? IpAddress,
    string MerchantId,
    string OrderId,
    string? ProjectCode,
    Dictionary<string, string>? Metadata = null)
{
    public static FraudCheckRequest Create(
        decimal amount,
        string currency,
        string paymentMethod,
        string merchantId,
        string orderId,
        string? customerEmail = null,
        string? customerPhone = null,
        string? customerId = null,
        string? deviceId = null,
        string? ipAddress = null,
        string? projectCode = null,
        Dictionary<string, string>? metadata = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be null or empty", nameof(currency));
        
        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new ArgumentException("Payment method cannot be null or empty", nameof(paymentMethod));
        
        if (string.IsNullOrWhiteSpace(merchantId))
            throw new ArgumentException("Merchant ID cannot be null or empty", nameof(merchantId));
        
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Order ID cannot be null or empty", nameof(orderId));

        return new FraudCheckRequest(
            amount,
            currency,
            paymentMethod,
            customerEmail,
            customerPhone,
            customerId,
            deviceId,
            ipAddress,
            merchantId,
            orderId,
            projectCode,
            metadata);
    }
}

/// <summary>
/// Result of fraud detection check.
/// Immutable record following DDD value object pattern.
/// </summary>
public sealed record FraudCheckResult(
    FraudRiskLevel RiskLevel,
    string Recommendation,
    decimal RiskScore,
    IReadOnlyList<string> Reasons,
    string? TransactionId = null)
{
    public bool IsHighRisk => RiskLevel == FraudRiskLevel.High;
    
    public bool IsMediumRisk => RiskLevel == FraudRiskLevel.Medium;
    
    public bool IsLowRisk => RiskLevel == FraudRiskLevel.Low;
    
    public bool ShouldBlock => RiskLevel == FraudRiskLevel.High;
    
    public bool ShouldReview => RiskLevel == FraudRiskLevel.Medium;
    
    public static FraudCheckResult LowRisk(string? transactionId = null) =>
        new(FraudRiskLevel.Low, "Approve", 0.0m, Array.Empty<string>(), transactionId);
    
    public static FraudCheckResult MediumRisk(decimal riskScore, IReadOnlyList<string> reasons, string? transactionId = null) =>
        new(FraudRiskLevel.Medium, "Review", riskScore, reasons, transactionId);
    
    public static FraudCheckResult HighRisk(decimal riskScore, IReadOnlyList<string> reasons, string? transactionId = null) =>
        new(FraudRiskLevel.High, "Block", riskScore, reasons, transactionId);
}

/// <summary>
/// Risk level for fraud detection.
/// </summary>
public enum FraudRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

