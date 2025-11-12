using Moq;
using Payment.Domain.Enums;
using Payment.Domain.Services;

namespace Payment.Domain.Tests.Helpers;

/// <summary>
/// Helper class for creating mock payment state services in tests.
/// </summary>
public static class MockPaymentStateService
{
    /// <summary>
    /// Creates a mock state service that allows all transitions.
    /// </summary>
    public static IPaymentStateService CreatePermissive()
    {
        var mock = new Mock<IPaymentStateService>();
        
        // Allow all transitions
        mock.Setup(s => s.CanTransition(It.IsAny<PaymentStatus>(), It.IsAny<PaymentTrigger>()))
            .Returns(true);
        
        // Return next state based on trigger
        mock.Setup(s => s.Transition(It.IsAny<PaymentStatus>(), It.IsAny<PaymentTrigger>()))
            .Returns<PaymentStatus, PaymentTrigger>((currentStatus, trigger) =>
            {
                return trigger switch
                {
                    PaymentTrigger.Process => PaymentStatus.Processing,
                    PaymentTrigger.Complete => PaymentStatus.Succeeded,
                    PaymentTrigger.Fail => PaymentStatus.Failed,
                    PaymentTrigger.Refund => PaymentStatus.Refunded,
                    PaymentTrigger.PartialRefund => PaymentStatus.PartiallyRefunded,
                    PaymentTrigger.Cancel => PaymentStatus.Cancelled,
                    _ => currentStatus
                };
            });
        
        return mock.Object;
    }

    /// <summary>
    /// Creates a mock state service that validates transitions using the actual state machine logic.
    /// </summary>
    public static IPaymentStateService CreateWithValidation()
    {
        var mock = new Mock<IPaymentStateService>();
        var factory = new Payment.Infrastructure.StateMachines.PaymentStateMachineFactory(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Payment.Infrastructure.StateMachines.PaymentStateMachine>.Instance);
        
        mock.Setup(s => s.CanTransition(It.IsAny<PaymentStatus>(), It.IsAny<PaymentTrigger>()))
            .Returns<PaymentStatus, PaymentTrigger>((currentStatus, trigger) =>
            {
                var stateMachine = factory.Create(currentStatus);
                return stateMachine.CanFire(trigger);
            });
        
        mock.Setup(s => s.Transition(It.IsAny<PaymentStatus>(), It.IsAny<PaymentTrigger>()))
            .Returns<PaymentStatus, PaymentTrigger>((currentStatus, trigger) =>
            {
                var stateMachine = factory.Create(currentStatus);
                if (!stateMachine.CanFire(trigger))
                {
                    throw new InvalidOperationException(
                        $"Cannot transition from {currentStatus} using trigger {trigger}");
                }
                stateMachine.Fire(trigger);
                return stateMachine.CurrentState;
            });
        
        return mock.Object;
    }
}

