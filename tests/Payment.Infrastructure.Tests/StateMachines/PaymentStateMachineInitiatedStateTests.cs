using FluentAssertions;
using Payment.Domain.Enums;
using Payment.Infrastructure.StateMachines;
using Xunit;

namespace Payment.Infrastructure.Tests.StateMachines;

/// <summary>
/// Tests for Initiated state in PaymentStateMachine (State Machine #18).
/// Ensures Initiated state transitions are properly handled.
/// </summary>
public class PaymentStateMachineInitiatedStateTests
{
    private readonly PaymentStateMachineFactory _factory;

    public PaymentStateMachineInitiatedStateTests()
    {
        _factory = new PaymentStateMachineFactory(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentStateMachine>.Instance);
    }

    [Fact]
    public void Initiated_ShouldAllowProcess_TransitionToProcessing()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act
        stateMachine.Fire(PaymentTrigger.Process);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Processing);
    }

    [Fact]
    public void Initiated_ShouldAllowFail_TransitionToFailed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act
        stateMachine.Fire(PaymentTrigger.Fail);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void Initiated_ShouldAllowCancel_TransitionToCancelled()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act
        stateMachine.Fire(PaymentTrigger.Cancel);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public void Initiated_ShouldNotAllowComplete_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Complete).Should().BeFalse();
        var act = () => stateMachine.Fire(PaymentTrigger.Complete);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot fire trigger {PaymentTrigger.Complete} from state {PaymentStatus.Initiated}");
    }

    [Fact]
    public void Initiated_ShouldNotAllowRefund_ThrowsException()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act & Assert
        stateMachine.CanFire(PaymentTrigger.Refund).Should().BeFalse();
        var act = () => stateMachine.Fire(PaymentTrigger.Refund);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Initiated_ShouldReturnCorrectPermittedTriggers()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act
        var permitted = stateMachine.PermittedTriggers.ToList();

        // Assert
        permitted.Should().Contain(PaymentTrigger.Process);
        permitted.Should().Contain(PaymentTrigger.Fail);
        permitted.Should().Contain(PaymentTrigger.Cancel);
        permitted.Should().NotContain(PaymentTrigger.Complete);
        permitted.Should().NotContain(PaymentTrigger.Refund);
        permitted.Should().NotContain(PaymentTrigger.PartialRefund);
    }

    [Fact]
    public void CompleteFlow_InitiatedToProcessingToSucceeded_ShouldSucceed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act & Assert
        stateMachine.Fire(PaymentTrigger.Process);
        stateMachine.CurrentState.Should().Be(PaymentStatus.Processing);

        stateMachine.Fire(PaymentTrigger.Complete);
        stateMachine.CurrentState.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public void FailureFlow_InitiatedToFailed_ShouldSucceed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act
        stateMachine.Fire(PaymentTrigger.Fail);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void CancellationFlow_InitiatedToCancelled_ShouldSucceed()
    {
        // Arrange
        var stateMachine = _factory.Create(PaymentStatus.Initiated);

        // Act
        stateMachine.Fire(PaymentTrigger.Cancel);

        // Assert
        stateMachine.CurrentState.Should().Be(PaymentStatus.Cancelled);
    }
}

