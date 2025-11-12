namespace Payment.Domain.Events;

public sealed record PaymentFailedEvent(
    Guid PaymentId,
    string OrderId,
    string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

