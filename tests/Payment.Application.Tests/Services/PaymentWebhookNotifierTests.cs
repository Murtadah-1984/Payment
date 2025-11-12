using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Services;

public class PaymentWebhookNotifierTests
{
    private readonly Mock<IWebhookDeliveryService> _webhookServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<PaymentWebhookNotifier>> _loggerMock;
    private readonly PaymentWebhookNotifier _notifier;

    public PaymentWebhookNotifierTests()
    {
        _webhookServiceMock = new Mock<IWebhookDeliveryService>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<PaymentWebhookNotifier>>();

        _notifier = new PaymentWebhookNotifier(
            _webhookServiceMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task NotifyPaymentStatusChangeAsync_ShouldScheduleWebhook_WhenWebhookUrlInMetadata()
    {
        // Arrange
        var payment = CreateTestPayment();
        payment.Metadata["webhook_url"] = "https://example.com/webhook";
        var eventType = "payment.completed";

        _webhookServiceMock
            .Setup(s => s.ScheduleWebhookAsync(
                payment.Id.Value,
                "https://example.com/webhook",
                eventType,
                It.IsAny<string>(),
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _notifier.NotifyPaymentStatusChangeAsync(payment, eventType);

        // Assert
        _webhookServiceMock.Verify(
            s => s.ScheduleWebhookAsync(
                payment.Id.Value,
                "https://example.com/webhook",
                eventType,
                It.IsAny<string>(),
                5,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyPaymentStatusChangeAsync_ShouldUseMerchantConfig_WhenNoMetadata()
    {
        // Arrange
        var payment = CreateTestPayment("merchant-123");
        var eventType = "payment.completed";

        _configurationMock
            .Setup(c => c["Webhooks:Merchants:merchant-123:Url"])
            .Returns("https://merchant.example.com/webhook");

        _webhookServiceMock
            .Setup(s => s.ScheduleWebhookAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _notifier.NotifyPaymentStatusChangeAsync(payment, eventType);

        // Assert
        _webhookServiceMock.Verify(
            s => s.ScheduleWebhookAsync(
                payment.Id.Value,
                "https://merchant.example.com/webhook",
                eventType,
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyPaymentStatusChangeAsync_ShouldUseDefaultUrl_WhenNoMetadataOrMerchantConfig()
    {
        // Arrange
        var payment = CreateTestPayment();
        var eventType = "payment.completed";

        _configurationMock
            .Setup(c => c["Webhooks:DefaultUrl"])
            .Returns("https://default.example.com/webhook");

        _webhookServiceMock
            .Setup(s => s.ScheduleWebhookAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _notifier.NotifyPaymentStatusChangeAsync(payment, eventType);

        // Assert
        _webhookServiceMock.Verify(
            s => s.ScheduleWebhookAsync(
                payment.Id.Value,
                "https://default.example.com/webhook",
                eventType,
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyPaymentStatusChangeAsync_ShouldNotSchedule_WhenNoWebhookUrlConfigured()
    {
        // Arrange
        var payment = CreateTestPayment();
        var eventType = "payment.completed";

        // No webhook URL in metadata, config, or default

        // Act
        await _notifier.NotifyPaymentStatusChangeAsync(payment, eventType);

        // Assert
        _webhookServiceMock.Verify(
            s => s.ScheduleWebhookAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyPaymentStatusChangeAsync_ShouldUseCustomMaxRetries_FromConfiguration()
    {
        // Arrange
        var payment = CreateTestPayment();
        payment.Metadata["webhook_url"] = "https://example.com/webhook";
        var eventType = "payment.completed";

        _configurationMock
            .Setup(c => c.GetValue<int>("Webhooks:MaxRetries", 5))
            .Returns(10);

        _webhookServiceMock
            .Setup(s => s.ScheduleWebhookAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _notifier.NotifyPaymentStatusChangeAsync(payment, eventType);

        // Assert
        _webhookServiceMock.Verify(
            s => s.ScheduleWebhookAsync(
                payment.Id.Value,
                "https://example.com/webhook",
                eventType,
                It.IsAny<string>(),
                10, // Custom max retries
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyPaymentStatusChangeAsync_ShouldCreateValidPayload_WithPaymentData()
    {
        // Arrange
        var payment = CreateTestPayment();
        payment.Metadata["webhook_url"] = "https://example.com/webhook";
        var eventType = "payment.completed";

        string? capturedPayload = null;
        _webhookServiceMock
            .Setup(s => s.ScheduleWebhookAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string, string, int, CancellationToken>(
                (id, url, evt, payload, retries, ct) => capturedPayload = payload)
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _notifier.NotifyPaymentStatusChangeAsync(payment, eventType);

        // Assert
        capturedPayload.Should().NotBeNull();
        capturedPayload.Should().Contain(payment.Id.Value.ToString());
        capturedPayload.Should().Contain(payment.OrderId);
        capturedPayload.Should().Contain(payment.MerchantId);
        capturedPayload.Should().Contain(payment.Amount.Value.ToString());
        capturedPayload.Should().Contain(payment.Currency.Code);
        capturedPayload.Should().Contain(payment.Status.ToString());
    }

    [Fact]
    public async Task NotifyPaymentStatusChangeAsync_ShouldHandleExceptions_Gracefully()
    {
        // Arrange
        var payment = CreateTestPayment();
        payment.Metadata["webhook_url"] = "https://example.com/webhook";
        var eventType = "payment.completed";

        _webhookServiceMock
            .Setup(s => s.ScheduleWebhookAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Webhook service error"));

        // Act
        var action = async () => await _notifier.NotifyPaymentStatusChangeAsync(payment, eventType);

        // Assert - Should not throw
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyPaymentStatusChangeAsync_ShouldPrioritizeMetadata_OverConfig()
    {
        // Arrange
        var payment = CreateTestPayment("merchant-123");
        payment.Metadata["webhook_url"] = "https://metadata.example.com/webhook";

        _configurationMock
            .Setup(c => c["Webhooks:Merchants:merchant-123:Url"])
            .Returns("https://merchant.example.com/webhook");

        _webhookServiceMock
            .Setup(s => s.ScheduleWebhookAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _notifier.NotifyPaymentStatusChangeAsync(payment, "payment.completed");

        // Assert
        _webhookServiceMock.Verify(
            s => s.ScheduleWebhookAsync(
                payment.Id.Value,
                "https://metadata.example.com/webhook", // Should use metadata URL
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static PaymentEntity CreateTestPayment(string merchantId = "merchant-123")
    {
        return new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            merchantId,
            "order-456");
    }
}

