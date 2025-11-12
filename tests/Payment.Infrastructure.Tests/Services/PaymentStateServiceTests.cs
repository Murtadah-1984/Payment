using FluentAssertions;
using Moq;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using Payment.Infrastructure.Services;
using Payment.Infrastructure.StateMachines;
using Xunit;

namespace Payment.Infrastructure.Tests.Services;

/// <summary>
/// Tests for PaymentStateService (State Machine #18).
/// Tests the service that bridges Domain and Infrastructure layers.
/// </summary>
public class PaymentStateServiceTests
{
    private readonly Mock<IPaymentStateMachineFactory> _factoryMock;
    private readonly PaymentStateService _service;

    public PaymentStateServiceTests()
    {
        _factoryMock = new Mock<IPaymentStateMachineFactory>();
        _service = new PaymentStateService(_factoryMock.Object);
    }

    [Fact]
    public void Transition_ShouldReturnNewState_WhenTransitionIsValid()
    {
        // Arrange
        var stateMachine = new PaymentStateMachine(
            PaymentStatus.Pending,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentStateMachine>.Instance);
        
        _factoryMock.Setup(f => f.Create(PaymentStatus.Pending))
            .Returns(stateMachine);

        // Act
        var newState = _service.Transition(PaymentStatus.Pending, PaymentTrigger.Process);

        // Assert
        newState.Should().Be(PaymentStatus.Processing);
    }

    [Fact]
    public void Transition_ShouldThrowException_WhenTransitionIsInvalid()
    {
        // Arrange
        var stateMachine = new PaymentStateMachine(
            PaymentStatus.Pending,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentStateMachine>.Instance);
        
        _factoryMock.Setup(f => f.Create(PaymentStatus.Pending))
            .Returns(stateMachine);

        // Act & Assert
        var act = () => _service.Transition(PaymentStatus.Pending, PaymentTrigger.Refund);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot transition from {PaymentStatus.Pending} using trigger {PaymentTrigger.Refund}");
    }

    [Fact]
    public void CanTransition_ShouldReturnTrue_WhenTransitionIsValid()
    {
        // Arrange
        var stateMachine = new PaymentStateMachine(
            PaymentStatus.Pending,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentStateMachine>.Instance);
        
        _factoryMock.Setup(f => f.Create(PaymentStatus.Pending))
            .Returns(stateMachine);

        // Act
        var canTransition = _service.CanTransition(PaymentStatus.Pending, PaymentTrigger.Process);

        // Assert
        canTransition.Should().BeTrue();
    }

    [Fact]
    public void CanTransition_ShouldReturnFalse_WhenTransitionIsInvalid()
    {
        // Arrange
        var stateMachine = new PaymentStateMachine(
            PaymentStatus.Pending,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentStateMachine>.Instance);
        
        _factoryMock.Setup(f => f.Create(PaymentStatus.Pending))
            .Returns(stateMachine);

        // Act
        var canTransition = _service.CanTransition(PaymentStatus.Pending, PaymentTrigger.Refund);

        // Assert
        canTransition.Should().BeFalse();
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentTrigger.Process, PaymentStatus.Processing)]
    [InlineData(PaymentStatus.Pending, PaymentTrigger.Fail, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Pending, PaymentTrigger.Cancel, PaymentStatus.Cancelled)]
    [InlineData(PaymentStatus.Processing, PaymentTrigger.Complete, PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.Processing, PaymentTrigger.Fail, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Succeeded, PaymentTrigger.Refund, PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.Succeeded, PaymentTrigger.PartialRefund, PaymentStatus.PartiallyRefunded)]
    [InlineData(PaymentStatus.PartiallyRefunded, PaymentTrigger.Refund, PaymentStatus.Refunded)]
    public void Transition_ShouldReturnCorrectState_ForValidTransitions(
        PaymentStatus currentStatus,
        PaymentTrigger trigger,
        PaymentStatus expectedStatus)
    {
        // Arrange
        var stateMachine = new PaymentStateMachine(
            currentStatus,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentStateMachine>.Instance);
        
        _factoryMock.Setup(f => f.Create(currentStatus))
            .Returns(stateMachine);

        // Act
        var newState = _service.Transition(currentStatus, trigger);

        // Assert
        newState.Should().Be(expectedStatus);
    }
}

