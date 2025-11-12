using FluentAssertions;
using Payment.Domain.Enums;
using Payment.Infrastructure.StateMachines;
using Xunit;

namespace Payment.Infrastructure.Tests.StateMachines;

/// <summary>
/// Comprehensive tests for PaymentStateMachine (State Machine #18).
/// Tests all valid and invalid state transitions.
/// </summary>
public class PaymentStateMachineTests
{
    private readonly PaymentStateMachineFactory _factory;

    public PaymentStateMachineTests()
    {
        _factory = new PaymentStateMachineFactory(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentStateMachine>.Instance);
    }

    #region Pending State Tests

    [Fact]
    public void Pending_ShouldAllowProcess_TransitionToProcessing()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Pending);

        // Act
        stateMachine.Fire(PaymentTrigger.Process);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Processing);
    }

    [Fact]
    public void Pending_ShouldAllowFail_TransitionToFailed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Pending);

        // Act
        stateMachine.Fire(PaymentTrigger.Fail);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void Pending_ShouldAllowCancel_TransitionToCancelled()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Pending);

        // Act
        stateMachine.Fire(PaymentTrigger.Cancel);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public void Pending_ShouldNotAllowComplete_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Pending);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Complete).Should().BeFalse();
        var act = () => stateMachine.Fire(PaymentTrigger.Complete);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot fire trigger {PaymentTrigger.Complete} from state {PaymentStatus.Pending}");
    }

    [Fact]
    public void Pending_ShouldNotAllowRefund_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Pending);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Refund).Should().BeFalse();
        var act = () => stateMachine.Fire(PaymentTrigger.Refund);
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Processing State Tests

    [Fact]
    public void Processing_ShouldAllowComplete_TransitionToSucceeded()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Processing);

        // Act
        stateMachine.Fire(PaymentTrigger.Complete);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public void Processing_ShouldAllowFail_TransitionToFailed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Processing);

        // Act
        stateMachine.Fire(PaymentTrigger.Fail);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void Processing_ShouldNotAllowProcess_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Processing);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Process).Should().BeFalse();
        var act = () => stateMachine.Fire(PaymentTrigger.Process);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Processing_ShouldNotAllowRefund_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Processing);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Refund).Should().BeFalse();
    }

    #endregion

    #region Succeeded State Tests

    [Fact]
    public void Succeeded_ShouldAllowRefund_TransitionToRefunded()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Succeeded);

        // Act
        stateMachine.Fire(PaymentTrigger.Refund);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void Succeeded_ShouldAllowPartialRefund_TransitionToPartiallyRefunded()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Succeeded);

        // Act
        stateMachine.Fire(PaymentTrigger.PartialRefund);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.PartiallyRefunded);
    }

    [Fact]
    public void Succeeded_ShouldNotAllowProcess_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Succeeded);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Process).Should().BeFalse();
    }

    [Fact]
    public void Succeeded_ShouldNotAllowComplete_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Succeeded);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Complete).Should().BeFalse();
    }

    [Fact]
    public void Succeeded_ShouldNotAllowFail_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Succeeded);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Fail).Should().BeFalse();
    }

    #endregion

    #region Failed State Tests

    [Fact]
    public void Failed_ShouldIgnoreFail_NoTransition()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Failed);

        // Act
        stateMachine.Fire(PaymentTrigger.Fail);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void Failed_ShouldNotAllowProcess_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Failed);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Process).Should().BeFalse();
    }

    [Fact]
    public void Failed_ShouldNotAllowRefund_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Failed);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Refund).Should().BeFalse();
    }

    #endregion

    #region Refunded State Tests

    [Fact]
    public void Refunded_ShouldIgnoreRefund_NoTransition()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Refunded);

        // Act
        stateMachine.Fire(PaymentTrigger.Refund);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void Refunded_ShouldNotAllowProcess_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Refunded);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Process).Should().BeFalse();
    }

    #endregion

    #region PartiallyRefunded State Tests

    [Fact]
    public void PartiallyRefunded_ShouldAllowRefund_TransitionToRefunded()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.PartiallyRefunded);

        // Act
        stateMachine.Fire(PaymentTrigger.Refund);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void PartiallyRefunded_ShouldNotAllowPartialRefund_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.PartiallyRefunded);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.PartialRefund).Should().BeFalse();
    }

    #endregion

    #region Cancelled State Tests

    [Fact]
    public void Cancelled_ShouldIgnoreCancel_NoTransition()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Cancelled);

        // Act
        stateMachine.Fire(PaymentTrigger.Cancel);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public void Cancelled_ShouldNotAllowProcess_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Cancelled);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Process).Should().BeFalse();
    }

    #endregion

    #region PermittedTriggers Tests

    [Fact]
    public void Pending_ShouldReturnCorrectPermittedTriggers()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Pending);

        // Act
        var permitted = stateMachine.PermittedTriggers.ToList();

        // Assert
        permitted.Should().Contain(PaymentTrigger.Process);
        permitted.Should().Contain(PaymentTrigger.Fail);
        permitted.Should().Contain(PaymentTrigger.Cancel);
        permitted.Should().NotContain(PaymentTrigger.Complete);
        permitted.Should().NotContain(PaymentTrigger.Refund);
    }

    [Fact]
    public void Processing_ShouldReturnCorrectPermittedTriggers()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Processing);

        // Act
        var permitted = stateMachine.PermittedTriggers.ToList();

        // Assert
        permitted.Should().Contain(PaymentTrigger.Complete);
        permitted.Should().Contain(PaymentTrigger.Fail);
        permitted.Should().NotContain(PaymentTrigger.Process);
        permitted.Should().NotContain(PaymentTrigger.Refund);
    }

    [Fact]
    public void Succeeded_ShouldReturnCorrectPermittedTriggers()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Succeeded);

        // Act
        var permitted = stateMachine.PermittedTriggers.ToList();

        // Assert
        permitted.Should().Contain(PaymentTrigger.Refund);
        permitted.Should().Contain(PaymentTrigger.PartialRefund);
        permitted.Should().NotContain(PaymentTrigger.Process);
        permitted.Should().NotContain(PaymentTrigger.Complete);
        permitted.Should().NotContain(PaymentTrigger.Fail);
    }

    #endregion

    #region State Transition Flow Tests

    [Fact]
    public void CompleteFlow_PendingToProcessingToSucceeded_ShouldSucceed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Pending);

        // Act & Assert
        stateMachine.Fire(PaymentTrigger.Process);
        stateMachine.CurrentState.Should().Be(PaymentStatus.Processing);

        stateMachine.Fire(PaymentTrigger.Complete);
        stateMachine.CurrentState.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public void FailureFlow_PendingToFailed_ShouldSucceed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Pending);

        // Act
        stateMachine.Fire(PaymentTrigger.Fail);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void RefundFlow_SucceededToRefunded_ShouldSucceed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Succeeded);

        // Act
        stateMachine.Fire(PaymentTrigger.Refund);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void PartialRefundFlow_SucceededToPartiallyRefundedToRefunded_ShouldSucceed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Succeeded);

        // Act & Assert
        stateMachine.Fire(PaymentTrigger.PartialRefund);
        stateMachine.CurrentState.Should().Be(PaymentStatus.PartiallyRefunded);

        stateMachine.Fire(PaymentTrigger.Refund);
        stateMachine.CurrentState.Should().Be(PaymentStatus.Refunded);
    }

    #endregion
}

