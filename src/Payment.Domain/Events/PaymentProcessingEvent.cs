namespace Payment.Domain.Events;

public sealed record PaymentProcessingEvent(
    Guid PaymentId,
    string OrderId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

