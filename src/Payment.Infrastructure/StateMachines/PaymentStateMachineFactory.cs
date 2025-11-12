using Microsoft.Extensions.Logging;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.StateMachines;

/// <summary>
/// Factory implementation for creating payment state machines.
/// Follows Factory pattern - creates state machine instances.
/// </summary>
public class PaymentStateMachineFactory : IPaymentStateMachineFactory
{
    private readonly ILogger<PaymentStateMachine> _logger;

    public PaymentStateMachineFactory(ILogger<PaymentStateMachine> logger)
    {
        _logger = logger;
    }

    public IPaymentStateMachine Create(PaymentStatus initialStatus)
    {
        return new PaymentStateMachine(initialStatus, _logger);
    }
}

