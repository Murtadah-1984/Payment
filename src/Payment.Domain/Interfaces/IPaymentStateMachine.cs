using Payment.Domain.Enums;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for payment state machine.
/// Enforces valid state transitions and prevents invalid operations.
/// Follows Dependency Inversion Principle - Domain defines the contract.
/// </summary>
public interface IPaymentStateMachine
{
    /// <summary>
    /// Gets the current state of the payment.
    /// </summary>
    PaymentStatus CurrentState { get; }

    /// <summary>
    /// Gets all permitted triggers from the current state.
    /// </summary>
    IEnumerable<PaymentTrigger> PermittedTriggers { get; }

    /// <summary>
    /// Checks if a trigger can be fired from the current state.
    /// </summary>
    /// <param name="trigger">The trigger to check.</param>
    /// <returns>True if the trigger can be fired, false otherwise.</returns>
    bool CanFire(PaymentTrigger trigger);

    /// <summary>
    /// Fires a trigger to transition to a new state.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <exception cref="InvalidOperationException">Thrown when the trigger cannot be fired from the current state.</exception>
    void Fire(PaymentTrigger trigger);
}

