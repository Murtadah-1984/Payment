namespace Payment.Domain.Events;

public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    string OrderId,
    decimal Amount,
    string Currency) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

