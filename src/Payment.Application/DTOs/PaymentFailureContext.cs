namespace Payment.Application.DTOs;

/// <summary>
/// Represents the context of a payment failure incident.
/// Immutable record following DDD value object pattern.
/// </summary>
public sealed record PaymentFailureContext(
    DateTime StartTime,
    DateTime? EndTime,
    string? Provider,
    Domain.Enums.PaymentFailureType FailureType,
    int AffectedPaymentCount,
    Dictionary<string, object> Metadata)
{
    /// <summary>
    /// Gets the duration of the incident if it has ended.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Checks if the incident is still ongoing.
    /// </summary>
    public bool IsOngoing => !EndTime.HasValue;
}

