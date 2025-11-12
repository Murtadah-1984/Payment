using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Implementation of payment state service using state machine.
/// Bridges Domain service interface with Infrastructure state machine implementation.
/// </summary>
public class PaymentStateService : IPaymentStateService
{
    private readonly IPaymentStateMachineFactory _stateMachineFactory;

    public PaymentStateService(IPaymentStateMachineFactory stateMachineFactory)
    {
        _stateMachineFactory = stateMachineFactory;
    }

    public PaymentStatus Transition(PaymentStatus currentStatus, PaymentTrigger trigger)
    {
        var stateMachine = _stateMachineFactory.Create(currentStatus);
        
        if (!stateMachine.CanFire(trigger))
        {
            throw new InvalidOperationException(
                $"Cannot transition from {currentStatus} using trigger {trigger}");
        }

        stateMachine.Fire(trigger);
        return stateMachine.CurrentState;
    }

    public bool CanTransition(PaymentStatus currentStatus, PaymentTrigger trigger)
    {
        var stateMachine = _stateMachineFactory.Create(currentStatus);
        return stateMachine.CanFire(trigger);
    }
}

