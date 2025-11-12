using Microsoft.Extensions.Logging;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Stateless;

namespace Payment.Infrastructure.StateMachines;

/// <summary>
/// State machine for payment status transitions.
/// Ensures valid state transitions and prevents invalid operations.
/// Implements IPaymentStateMachine interface from Domain layer (Dependency Inversion Principle).
/// </summary>
public class PaymentStateMachine : IPaymentStateMachine
{
    private readonly StateMachine<PaymentStatus, PaymentTrigger> _stateMachine;
    private readonly ILogger<PaymentStateMachine>? _logger;

    public PaymentStateMachine(PaymentStatus initialState, ILogger<PaymentStateMachine>? logger = null)
    {
        _logger = logger;
        _stateMachine = new StateMachine<PaymentStatus, PaymentTrigger>(initialState);

        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        // Initiated state (initial state when payment is first created)
        _stateMachine.Configure(PaymentStatus.Initiated)
            .Permit(PaymentTrigger.Process, PaymentStatus.Processing)
            .Permit(PaymentTrigger.Fail, PaymentStatus.Failed)
            .Permit(PaymentTrigger.Cancel, PaymentStatus.Cancelled)
            .OnEntry(() => _logger?.LogDebug("Payment entered Initiated state"))
            .OnExit(() => _logger?.LogDebug("Payment exited Initiated state"));

        // Pending state
        _stateMachine.Configure(PaymentStatus.Pending)
            .Permit(PaymentTrigger.Process, PaymentStatus.Processing)
            .Permit(PaymentTrigger.Fail, PaymentStatus.Failed)
            .Permit(PaymentTrigger.Cancel, PaymentStatus.Cancelled)
            .OnEntry(() => _logger?.LogDebug("Payment entered Pending state"))
            .OnExit(() => _logger?.LogDebug("Payment exited Pending state"));

        // Processing state
        _stateMachine.Configure(PaymentStatus.Processing)
            .Permit(PaymentTrigger.Complete, PaymentStatus.Succeeded)
            .Permit(PaymentTrigger.Fail, PaymentStatus.Failed)
            .OnEntry(() => _logger?.LogDebug("Payment entered Processing state"))
            .OnExit(() => _logger?.LogDebug("Payment exited Processing state"));

        // Succeeded/Completed state
        _stateMachine.Configure(PaymentStatus.Succeeded)
            .Permit(PaymentTrigger.Refund, PaymentStatus.Refunded)
            .Permit(PaymentTrigger.PartialRefund, PaymentStatus.PartiallyRefunded)
            .OnEntry(() => _logger?.LogDebug("Payment entered Succeeded state"))
            .OnExit(() => _logger?.LogDebug("Payment exited Succeeded state"));

        // Failed state (terminal)
        _stateMachine.Configure(PaymentStatus.Failed)
            .Ignore(PaymentTrigger.Fail) // Already failed
            .OnEntry(() => _logger?.LogDebug("Payment entered Failed state"));

        // Refunded state (terminal)
        _stateMachine.Configure(PaymentStatus.Refunded)
            .Ignore(PaymentTrigger.Refund) // Already refunded
            .OnEntry(() => _logger?.LogDebug("Payment entered Refunded state"));

        // PartiallyRefunded state
        _stateMachine.Configure(PaymentStatus.PartiallyRefunded)
            .Permit(PaymentTrigger.Refund, PaymentStatus.Refunded)
            .OnEntry(() => _logger?.LogDebug("Payment entered PartiallyRefunded state"));

        // Cancelled state (terminal)
        _stateMachine.Configure(PaymentStatus.Cancelled)
            .Ignore(PaymentTrigger.Cancel) // Already cancelled
            .OnEntry(() => _logger?.LogDebug("Payment entered Cancelled state"));
    }

    public bool CanFire(PaymentTrigger trigger)
    {
        return _stateMachine.CanFire(trigger);
    }

    public void Fire(PaymentTrigger trigger)
    {
        if (!CanFire(trigger))
        {
            throw new InvalidOperationException(
                $"Cannot fire trigger {trigger} from state {_stateMachine.State}");
        }

        _stateMachine.Fire(trigger);
        _logger?.LogInformation("Payment state changed: {Trigger} -> {NewState}", trigger, _stateMachine.State);
    }

    public PaymentStatus CurrentState => _stateMachine.State;

    public IEnumerable<PaymentTrigger> PermittedTriggers => _stateMachine.PermittedTriggers;
}

