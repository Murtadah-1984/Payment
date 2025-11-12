namespace Payment.Application.DTOs;

/// <summary>
/// Request DTO for assessing payment failure incidents.
/// </summary>
public sealed record AssessPaymentFailureRequest(
    string? Provider,
    string FailureType,
    DateTime? StartTime,
    DateTime? EndTime,
    Dictionary<string, object>? Metadata = null)
{
    public PaymentFailureContext ToContext(int affectedPaymentCount)
    {
        return new PaymentFailureContext(
            StartTime: StartTime ?? DateTime.UtcNow.AddHours(-1),
            EndTime: EndTime,
            Provider: Provider,
            FailureType: Enum.TryParse<Domain.Enums.PaymentFailureType>(FailureType, out var type) 
                ? type 
                : Domain.Enums.PaymentFailureType.ProviderError,
            AffectedPaymentCount: affectedPaymentCount,
            Metadata: Metadata ?? new Dictionary<string, object>());
    }
}

