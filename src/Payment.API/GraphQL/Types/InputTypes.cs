namespace Payment.API.GraphQL.Types;

/// <summary>
/// GraphQL input type for creating a payment.
/// Follows Clean Architecture - maps to Application layer CreatePaymentDto.
/// </summary>
public class CreatePaymentInput
{
    public Guid RequestId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal? SystemFeePercent { get; set; }
    public SplitRuleInput? SplitRule { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public string? CallbackUrl { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? NfcToken { get; set; }
    public string? DeviceId { get; set; }
    public string? CustomerId { get; set; }
}

/// <summary>
/// GraphQL input type for split payment rules.
/// </summary>
public class SplitRuleInput
{
    public decimal SystemFeePercent { get; set; }
    public IReadOnlyCollection<SplitAccountInput> Accounts { get; set; } = Array.Empty<SplitAccountInput>();
}

/// <summary>
/// GraphQL input type for split account configuration.
/// </summary>
public class SplitAccountInput
{
    public string AccountType { get; set; } = string.Empty;
    public string AccountIdentifier { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
}

