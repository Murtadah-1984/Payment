namespace Payment.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

