using System.Reflection;
using FluentAssertions;
using Payment.Domain.Entities;
using Xunit;

namespace Payment.Domain.Tests.Entities;

public class WebhookDeliveryTests
{
    [Fact]
    public void Constructor_ShouldCreateWebhookDelivery_WithValidParameters()
    {
        // Arrange
        var id = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        // Act
        var webhook = new WebhookDelivery(id, paymentId, webhookUrl, eventType, payload);

        // Assert
        webhook.Id.Should().Be(id);
        webhook.PaymentId.Should().Be(paymentId);
        webhook.WebhookUrl.Should().Be(webhookUrl);
        webhook.EventType.Should().Be(eventType);
        webhook.Payload.Should().Be(payload);
        webhook.Status.Should().Be(WebhookDeliveryStatus.Pending);
        webhook.RetryCount.Should().Be(0);
        webhook.MaxRetries.Should().Be(5);
        webhook.NextRetryAt.Should().NotBeNull();
        webhook.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenPaymentIdIsEmpty()
    {
        // Arrange
        var id = Guid.NewGuid();
        var paymentId = Guid.Empty;
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        // Act & Assert
        var action = () => new WebhookDelivery(id, paymentId, webhookUrl, eventType, payload);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Payment ID cannot be empty*");
    }

    [Fact]
    public void Constructor_ShouldThrowException_WhenWebhookUrlIsNullOrEmpty()
    {
        // Arrange
        var id = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";

        // Act & Assert
        var action1 = () => new WebhookDelivery(id, paymentId, null!, eventType, payload);
        action1.Should().Throw<ArgumentException>()
            .WithMessage("Webhook URL cannot be null or empty*");

        var action2 = () => new WebhookDelivery(id, paymentId, string.Empty, eventType, payload);
        action2.Should().Throw<ArgumentException>()
            .WithMessage("Webhook URL cannot be null or empty*");
    }

    [Fact]
    public void Constructor_ShouldUseCustomMaxRetries_WhenProvided()
    {
        // Arrange
        var id = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var webhookUrl = "https://example.com/webhook";
        var eventType = "payment.completed";
        var payload = "{\"paymentId\":\"123\"}";
        var maxRetries = 10;

        // Act
        var webhook = new WebhookDelivery(id, paymentId, webhookUrl, eventType, payload, maxRetries);

        // Assert
        webhook.MaxRetries.Should().Be(10);
    }

    [Fact]
    public void MarkAsDelivered_ShouldUpdateStatus_AndSetDeliveredAt()
    {
        // Arrange
        var webhook = CreateTestWebhook();

        // Act
        webhook.MarkAsDelivered();

        // Assert
        webhook.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        webhook.DeliveredAt.Should().NotBeNull();
        webhook.DeliveredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        webhook.LastAttemptedAt.Should().NotBeNull();
        webhook.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkAsFailed_ShouldUpdateStatus_AndScheduleNextRetry_WhenRetriesRemaining()
    {
        // Arrange
        var webhook = CreateTestWebhook();
        var error = "HTTP 500: Internal Server Error";
        var httpStatusCode = 500;

        // Act
        webhook.MarkAsFailed(error, httpStatusCode);

        // Assert
        webhook.Status.Should().Be(WebhookDeliveryStatus.Pending);
        webhook.RetryCount.Should().Be(1);
        webhook.LastError.Should().Be(error);
        webhook.LastHttpStatusCode.Should().Be(httpStatusCode);
        webhook.LastAttemptedAt.Should().NotBeNull();
        webhook.NextRetryAt.Should().NotBeNull();
        webhook.NextRetryAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void MarkAsFailed_ShouldApplyExponentialBackoff_OnRetries()
    {
        // Arrange
        var webhook = CreateTestWebhook();
        var initialNextRetry = webhook.NextRetryAt!.Value;

        // Act - First retry
        webhook.MarkAsFailed("Error 1", 500);
        var firstRetryDelay = webhook.NextRetryAt!.Value - DateTime.UtcNow;

        // Second retry
        webhook.MarkAsFailed("Error 2", 500);
        var secondRetryDelay = webhook.NextRetryAt!.Value - DateTime.UtcNow;

        // Third retry
        webhook.MarkAsFailed("Error 3", 500);
        var thirdRetryDelay = webhook.NextRetryAt!.Value - DateTime.UtcNow;

        // Assert - Each retry should have exponentially increasing delay
        secondRetryDelay.Should().BeGreaterThan(firstRetryDelay);
        thirdRetryDelay.Should().BeGreaterThan(secondRetryDelay);
    }

    [Fact]
    public void MarkAsFailed_ShouldCapDelayAtOneHour()
    {
        // Arrange
        var webhook = new WebhookDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://example.com/webhook",
            "payment.completed",
            "{}",
            maxRetries: 20, // High number to test cap
            initialRetryDelay: TimeSpan.FromMinutes(30)); // Start with 30 minutes

        // Act - Retry multiple times to exceed 1 hour
        for (int i = 0; i < 5; i++)
        {
            webhook.MarkAsFailed($"Error {i}", 500);
        }

        // Assert - Delay should be capped at 1 hour
        var delay = webhook.NextRetryAt!.Value - DateTime.UtcNow;
        delay.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(1.1)); // Allow small margin
    }

    [Fact]
    public void MarkAsFailed_ShouldMarkAsFailed_WhenMaxRetriesExceeded()
    {
        // Arrange
        var webhook = new WebhookDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://example.com/webhook",
            "payment.completed",
            "{\"paymentId\":\"123\"}",
            maxRetries: 3);

        // Act - Exceed max retries
        webhook.MarkAsFailed("Error 1", 500);
        webhook.MarkAsFailed("Error 2", 500);
        webhook.MarkAsFailed("Error 3", 500);
        webhook.MarkAsFailed("Error 4", 500); // This should mark as failed

        // Assert
        webhook.Status.Should().Be(WebhookDeliveryStatus.Failed);
        webhook.RetryCount.Should().Be(4);
        webhook.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void IsReadyForRetry_ShouldReturnTrue_WhenPendingAndTimeReached()
    {
        // Arrange
        var webhook = CreateTestWebhook();
        // Set NextRetryAt to past time by marking as failed and then using reflection
        // Since NextRetryAt is set by MarkAsFailed, we'll create a webhook that's already ready
        webhook.MarkAsFailed("Test error", 500);
        // Use reflection to set NextRetryAt to past time for testing
        var nextRetryAtProperty = typeof(WebhookDelivery).GetProperty("NextRetryAt", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        nextRetryAtProperty?.SetValue(webhook, DateTime.UtcNow.AddSeconds(-1));

        // Act
        var result = webhook.IsReadyForRetry;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsReadyForRetry_ShouldReturnFalse_WhenStatusIsNotPending()
    {
        // Arrange
        var webhook = CreateTestWebhook();
        webhook.MarkAsDelivered();
        // NextRetryAt is not relevant when status is Delivered, but we can verify the test logic
        // The IsReadyForRetry property checks Status first, so this should return false

        // Act
        var result = webhook.IsReadyForRetry;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsReadyForRetry_ShouldReturnFalse_WhenRetryCountExceedsMax()
    {
        // Arrange
        var webhook = new WebhookDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://example.com/webhook",
            "payment.completed",
            "{\"paymentId\":\"123\"}",
            maxRetries: 2);
        webhook.MarkAsFailed("Error 1", 500);
        webhook.MarkAsFailed("Error 2", 500);
        webhook.MarkAsFailed("Error 3", 500); // Exceeds max

        // Act
        var result = webhook.IsReadyForRetry;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExhausted_ShouldReturnTrue_WhenStatusIsFailed()
    {
        // Arrange
        var webhook = new WebhookDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://example.com/webhook",
            "payment.completed",
            "{\"paymentId\":\"123\"}",
            maxRetries: 1);
        webhook.MarkAsFailed("Error", 500);
        webhook.MarkAsFailed("Error", 500); // Exceeds max

        // Act
        var result = webhook.IsExhausted;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsExhausted_ShouldReturnTrue_WhenRetryCountExceedsMax()
    {
        // Arrange
        var webhook = new WebhookDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://example.com/webhook",
            "payment.completed",
            "{\"paymentId\":\"123\"}",
            maxRetries: 2);
        webhook.MarkAsFailed("Error 1", 500);
        webhook.MarkAsFailed("Error 2", 500);
        webhook.MarkAsFailed("Error 3", 500);

        // Act
        var result = webhook.IsExhausted;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MarkAsFailed_ShouldThrowException_WhenErrorIsNullOrEmpty()
    {
        // Arrange
        var webhook = CreateTestWebhook();

        // Act & Assert
        var action1 = () => webhook.MarkAsFailed(null!, 500);
        action1.Should().Throw<ArgumentException>()
            .WithMessage("Error message cannot be null or empty*");

        var action2 = () => webhook.MarkAsFailed(string.Empty, 500);
        action2.Should().Throw<ArgumentException>()
            .WithMessage("Error message cannot be null or empty*");
    }

    private static WebhookDelivery CreateTestWebhook()
    {
        return new WebhookDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://example.com/webhook",
            "payment.completed",
            "{\"paymentId\":\"123\"}");
    }
}

