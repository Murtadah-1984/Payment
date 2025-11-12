namespace Payment.Domain.Enums;

/// <summary>
/// Triggers for payment state transitions.
/// Used by the payment state machine to enforce valid state transitions.
/// </summary>
public enum PaymentTrigger
{
    Process,
    Complete,
    Fail,
    Refund,
    PartialRefund,
    Cancel
}

