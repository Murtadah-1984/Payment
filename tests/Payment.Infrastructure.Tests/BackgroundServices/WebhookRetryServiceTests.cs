using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.BackgroundServices;
using Xunit;

namespace Payment.Infrastructure.Tests.BackgroundServices;

public class WebhookRetryServiceTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<WebhookRetryService>> _loggerMock;
    private readonly Mock<IWebhookDeliveryRepository> _repositoryMock;
    private readonly Mock<IWebhookDeliveryService> _webhookServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly WebhookRetryService _service;

    public WebhookRetryServiceTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<WebhookRetryService>>();
        _repositoryMock = new Mock<IWebhookDeliveryRepository>();
        _webhookServiceMock = new Mock<IWebhookDeliveryService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        var serviceScopeMock = new Mock<IServiceScope>();
        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();

        serviceScopeMock.Setup(s => s.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>())
            .Returns(_repositoryMock.Object);
        serviceScopeMock.Setup(s => s.ServiceProvider.GetRequiredService<IWebhookDeliveryService>())
            .Returns(_webhookServiceMock.Object);
        serviceScopeMock.Setup(s => s.ServiceProvider.GetRequiredService<IUnitOfWork>())
            .Returns(_unitOfWorkMock.Object);

        serviceScopeFactoryMock.Setup(f => f.CreateScope())
            .Returns(serviceScopeMock.Object);

        _serviceProviderMock.Setup(p => p.GetRequiredService<IServiceScopeFactory>())
            .Returns(serviceScopeFactoryMock.Object);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new WebhookRetryService(_serviceProviderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessPendingWebhooksAsync_ShouldRetryPendingWebhooks_WhenReadyForRetry()
    {
        // Arrange
        var webhook = CreatePendingWebhook();
        webhook.NextRetryAt = DateTime.UtcNow.AddSeconds(-1); // Ready for retry

        _repositoryMock
            .Setup(r => r.GetPendingRetriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { webhook });

        _webhookServiceMock
            .Setup(s => s.SendWebhookAsync(
                webhook.WebhookUrl,
                webhook.EventType,
                webhook.Payload,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookDeliveryResult(true, 200));

        // Act
        var method = typeof(WebhookRetryService).GetMethod(
            "ProcessPendingWebhooksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _webhookServiceMock.Verify(
            s => s.SendWebhookAsync(
                webhook.WebhookUrl,
                webhook.EventType,
                webhook.Payload,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.Is<WebhookDelivery>(w => w.Status == WebhookDeliveryStatus.Delivered), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingWebhooksAsync_ShouldMarkAsFailed_WhenRetryFails()
    {
        // Arrange
        var webhook = CreatePendingWebhook();
        webhook.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);

        _repositoryMock
            .Setup(r => r.GetPendingRetriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { webhook });

        _webhookServiceMock
            .Setup(s => s.SendWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookDeliveryResult(false, 500, "Internal Server Error"));

        // Act
        var method = typeof(WebhookRetryService).GetMethod(
            "ProcessPendingWebhooksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _repositoryMock.Verify(
            r => r.UpdateAsync(It.Is<WebhookDelivery>(w => w.Status == WebhookDeliveryStatus.Pending && w.RetryCount == 2), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingWebhooksAsync_ShouldNotProcess_WhenNoPendingWebhooks()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetPendingRetriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<WebhookDelivery>());

        // Act
        var method = typeof(WebhookRetryService).GetMethod(
            "ProcessPendingWebhooksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _webhookServiceMock.Verify(
            s => s.SendWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPendingWebhooksAsync_ShouldHandleExceptions_Gracefully()
    {
        // Arrange
        var webhook = CreatePendingWebhook();
        webhook.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);

        _repositoryMock
            .Setup(r => r.GetPendingRetriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { webhook });

        _webhookServiceMock
            .Setup(s => s.SendWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var method = typeof(WebhookRetryService).GetMethod(
            "ProcessPendingWebhooksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task; // Should not throw

        // Assert
        _repositoryMock.Verify(
            r => r.UpdateAsync(It.Is<WebhookDelivery>(w => w.LastError == "Network error"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingWebhooksAsync_ShouldProcessBatch_WhenMultipleWebhooksReady()
    {
        // Arrange
        var webhooks = new[]
        {
            CreatePendingWebhook(),
            CreatePendingWebhook(),
            CreatePendingWebhook()
        };

        foreach (var webhook in webhooks)
        {
            webhook.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
        }

        _repositoryMock
            .Setup(r => r.GetPendingRetriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhooks);

        _webhookServiceMock
            .Setup(s => s.SendWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookDeliveryResult(true, 200));

        // Act
        var method = typeof(WebhookRetryService).GetMethod(
            "ProcessPendingWebhooksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _webhookServiceMock.Verify(
            s => s.SendWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessPendingWebhooksAsync_ShouldRespectBatchSizeLimit()
    {
        // Arrange
        var webhooks = Enumerable.Range(0, 100)
            .Select(_ => CreatePendingWebhook())
            .ToList();

        foreach (var webhook in webhooks)
        {
            webhook.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
        }

        _repositoryMock
            .Setup(r => r.GetPendingRetriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhooks);

        _webhookServiceMock
            .Setup(s => s.SendWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookDeliveryResult(true, 200));

        // Act
        var method = typeof(WebhookRetryService).GetMethod(
            "ProcessPendingWebhooksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert - Should only process batch size (50) webhooks
        _webhookServiceMock.Verify(
            s => s.SendWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Exactly(50)); // Batch size limit
    }

    private static WebhookDelivery CreatePendingWebhook()
    {
        var webhook = new WebhookDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://example.com/webhook",
            "payment.completed",
            "{\"paymentId\":\"123\"}");

        webhook.MarkAsFailed("Initial failure", 500);
        return webhook;
    }
}

