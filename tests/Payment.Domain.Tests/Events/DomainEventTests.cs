using FluentAssertions;
using Payment.Domain.Events;
using Xunit;

namespace Payment.Domain.Tests.Events;

public class DomainEventTests
{
    [Fact]
    public void PaymentProcessingEvent_ShouldHaveCorrectProperties()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = "order-123";

        // Act
        var evt = new PaymentProcessingEvent(paymentId, orderId);

        // Assert
        evt.PaymentId.Should().Be(paymentId);
        evt.OrderId.Should().Be(orderId);
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PaymentCompletedEvent_ShouldHaveCorrectProperties()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = "order-123";
        var amount = 100.50m;
        var currency = "USD";

        // Act
        var evt = new PaymentCompletedEvent(paymentId, orderId, amount, currency);

        // Assert
        evt.PaymentId.Should().Be(paymentId);
        evt.OrderId.Should().Be(orderId);
        evt.Amount.Should().Be(amount);
        evt.Currency.Should().Be(currency);
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PaymentFailedEvent_ShouldHaveCorrectProperties()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = "order-123";
        var reason = "Insufficient funds";

        // Act
        var evt = new PaymentFailedEvent(paymentId, orderId, reason);

        // Assert
        evt.PaymentId.Should().Be(paymentId);
        evt.OrderId.Should().Be(orderId);
        evt.Reason.Should().Be(reason);
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PaymentRefundedEvent_ShouldHaveCorrectProperties()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = "order-123";
        var amount = 100.50m;
        var currency = "USD";

        // Act
        var evt = new PaymentRefundedEvent(paymentId, orderId, amount, currency);

        // Assert
        evt.PaymentId.Should().Be(paymentId);
        evt.OrderId.Should().Be(orderId);
        evt.Amount.Should().Be(amount);
        evt.Currency.Should().Be(currency);
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

