using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Domain.Entities;
using Payment.Domain.Events;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.BackgroundServices;
using System.Text.Json;
using Xunit;

namespace Payment.Infrastructure.Tests.BackgroundServices;

public class OutboxProcessorServiceTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<OutboxProcessorService>> _loggerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IOutboxMessageRepository> _outboxRepositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly OutboxProcessorService _service;

    public OutboxProcessorServiceTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<OutboxProcessorService>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _outboxRepositoryMock = new Mock<IOutboxMessageRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();

        _unitOfWorkMock.Setup(u => u.OutboxMessages).Returns(_outboxRepositoryMock.Object);

        var serviceScopeMock = new Mock<IServiceScope>();
        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        
        serviceScopeMock.Setup(s => s.ServiceProvider.GetRequiredService<IUnitOfWork>())
            .Returns(_unitOfWorkMock.Object);
        serviceScopeMock.Setup(s => s.ServiceProvider.GetRequiredService<IEventPublisher>())
            .Returns(_eventPublisherMock.Object);
        
        serviceScopeFactoryMock.Setup(f => f.CreateScope())
            .Returns(serviceScopeMock.Object);
        
        _serviceProviderMock.Setup(p => p.GetRequiredService<IServiceScopeFactory>())
            .Returns(serviceScopeFactoryMock.Object);

        _service = new OutboxProcessorService(_serviceProviderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WithPendingMessages_PublishesEvents()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = "order-123";
        var domainEvent = new PaymentCompletedEvent(paymentId, orderId, 100m, "USD");
        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(PaymentCompletedEvent),
            Payload = payload,
            Topic = "payment.events",
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        _outboxRepositoryMock
            .Setup(r => r.GetPendingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { outboxMessage });

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var method = typeof(OutboxProcessorService).GetMethod(
            "ProcessOutboxMessagesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _eventPublisherMock.Verify(
            p => p.PublishAsync("payment.events", It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _outboxRepositoryMock.Verify(
            r => r.MarkAsProcessedAsync(outboxMessage.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WithNoPendingMessages_DoesNotPublish()
    {
        // Arrange
        _outboxRepositoryMock
            .Setup(r => r.GetPendingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<OutboxMessage>());

        // Act
        var method = typeof(OutboxProcessorService).GetMethod(
            "ProcessOutboxMessagesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _eventPublisherMock.Verify(
            p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WithUnknownEventType_MarksAsFailed()
    {
        // Arrange
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "UnknownEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        _outboxRepositoryMock
            .Setup(r => r.GetPendingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { outboxMessage });

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var method = typeof(OutboxProcessorService).GetMethod(
            "ProcessOutboxMessagesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _outboxRepositoryMock.Verify(
            r => r.MarkAsFailedAsync(
                outboxMessage.Id,
                It.Is<string>(s => s.Contains("Unknown event type")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _eventPublisherMock.Verify(
            p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WithPublishException_MarksAsFailed()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = "order-123";
        var domainEvent = new PaymentCompletedEvent(paymentId, orderId, 100m, "USD");
        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(PaymentCompletedEvent),
            Payload = payload,
            Topic = "payment.events",
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        _outboxRepositoryMock
            .Setup(r => r.GetPendingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { outboxMessage });

        _eventPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Publish failed"));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var method = typeof(OutboxProcessorService).GetMethod(
            "ProcessOutboxMessagesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _outboxRepositoryMock.Verify(
            r => r.MarkAsFailedAsync(
                outboxMessage.Id,
                It.Is<string>(s => s.Contains("Publish failed")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WithMaxRetries_LogsError()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var orderId = "order-123";
        var domainEvent = new PaymentCompletedEvent(paymentId, orderId, 100m, "USD");
        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(PaymentCompletedEvent),
            Payload = payload,
            Topic = "payment.events",
            CreatedAt = DateTime.UtcNow,
            RetryCount = 5 // Max retries
        };

        _outboxRepositoryMock
            .Setup(r => r.GetPendingAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { outboxMessage });

        _eventPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Publish failed"));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var method = typeof(OutboxProcessorService).GetMethod(
            "ProcessOutboxMessagesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var task = (Task)method!.Invoke(_service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        _outboxRepositoryMock.Verify(
            r => r.MarkAsFailedAsync(
                outboxMessage.Id,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

