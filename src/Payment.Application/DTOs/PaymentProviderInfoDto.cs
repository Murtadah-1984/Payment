namespace Payment.Application.DTOs;

/// <summary>
/// DTO for payment provider information returned by the API.
/// Follows Clean Architecture - Application layer DTO for presentation.
/// </summary>
public sealed record PaymentProviderInfoDto(
    string ProviderName,
    string CountryCode,
    string Currency,
    string PaymentMethod,
    bool IsActive);

