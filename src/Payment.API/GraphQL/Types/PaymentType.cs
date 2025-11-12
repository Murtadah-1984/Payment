namespace Payment.API.GraphQL.Types;

/// <summary>
/// GraphQL type for PaymentDto.
/// Follows Clean Architecture - maps Application layer DTOs to GraphQL schema.
/// </summary>
public class PaymentType
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? FailureReason { get; set; }
    public SplitPaymentType? SplitPayment { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public CardTokenType? CardToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// GraphQL type for SplitPaymentDto.
/// </summary>
public class SplitPaymentType
{
    public decimal SystemShare { get; set; }
    public decimal OwnerShare { get; set; }
    public decimal SystemFeePercent { get; set; }
}

/// <summary>
/// GraphQL type for CardTokenDto (PCI DSS compliant - no sensitive data).
/// </summary>
public class CardTokenType
{
    public string Last4Digits { get; set; } = string.Empty;
    public string CardBrand { get; set; } = string.Empty;
}

