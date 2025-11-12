using Payment.Domain.Enums;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Factory interface for creating payment state machines.
/// Follows Factory pattern and Dependency Inversion Principle.
/// </summary>
public interface IPaymentStateMachineFactory
{
    /// <summary>
    /// Creates a new state machine instance for a payment with the given initial status.
    /// </summary>
    /// <param name="initialStatus">The initial status of the payment.</param>
    /// <returns>A new state machine instance.</returns>
    IPaymentStateMachine Create(PaymentStatus initialStatus);
}

