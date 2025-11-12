namespace Payment.Application.DTOs;

public sealed record PaymentDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Provider,
    string MerchantId,
    string OrderId,
    string Status,
    string? TransactionId,
    string? FailureReason,
    SplitPaymentDto? SplitPayment,
    Dictionary<string, string>? Metadata,
    CardTokenDto? CardToken,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    SettlementDto? Settlement = null,
    ThreeDSecureStatusDto? ThreeDSecure = null);

public sealed record SplitPaymentDto(
    decimal SystemShare,
    decimal OwnerShare,
    decimal SystemFeePercent);

/// <summary>
/// DTO for card token information (PCI DSS compliance - tokenization).
/// Never contains full card numbers, CVV, expiration dates, or PINs.
/// </summary>
public sealed record CardTokenDto(
    string Last4Digits,
    string CardBrand);

/// <summary>
/// DTO for multi-currency settlement information (Multi-Currency Settlement #21).
/// Contains converted amount and exchange rate when payment currency differs from settlement currency.
/// </summary>
public sealed record SettlementDto(
    string Currency,
    decimal Amount,
    decimal ExchangeRate,
    DateTime SettledAt);

