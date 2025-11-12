using Payment.Domain.Enums;

namespace Payment.Domain.Services;

/// <summary>
/// Domain service for managing payment state transitions.
/// Encapsulates state machine logic while keeping entities clean.
/// </summary>
public interface IPaymentStateService
{
    /// <summary>
    /// Validates if a state transition is allowed and returns the new state.
    /// </summary>
    /// <param name="currentStatus">The current payment status.</param>
    /// <param name="trigger">The trigger to apply.</param>
    /// <returns>The new status after the transition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the transition is not allowed.</exception>
    PaymentStatus Transition(PaymentStatus currentStatus, PaymentTrigger trigger);

    /// <summary>
    /// Checks if a state transition is allowed.
    /// </summary>
    /// <param name="currentStatus">The current payment status.</param>
    /// <param name="trigger">The trigger to check.</param>
    /// <returns>True if the transition is allowed, false otherwise.</returns>
    bool CanTransition(PaymentStatus currentStatus, PaymentTrigger trigger);
}

