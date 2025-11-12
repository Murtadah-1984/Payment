namespace Payment.Application.DTOs;

/// <summary>
/// DTO representing the result of a foreign exchange conversion.
/// Immutable record following DDD principles.
/// </summary>
public sealed record FxConversionResultDto(
    decimal OriginalAmount,
    decimal ConvertedAmount,
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateTime Timestamp);

