using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Infrastructure.Data;
using Payment.Infrastructure.Repositories;
using Xunit;

namespace Payment.Infrastructure.Tests.Repositories;

public class OutboxMessageRepositoryTests : IDisposable
{
    private readonly PaymentDbContext _context;
    private readonly OutboxMessageRepository _repository;

    public OutboxMessageRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PaymentDbContext(options);
        _repository = new OutboxMessageRepository(_context);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyUnprocessedMessages()
    {
        // Arrange
        var processedMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ProcessedAt = DateTime.UtcNow.AddMinutes(-5),
            RetryCount = 0
        };

        var pendingMessage1 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessedAt = null,
            RetryCount = 0
        };

        var pendingMessage2 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentFailedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            ProcessedAt = null,
            RetryCount = 2
        };

        var exceededRetriesMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ProcessedAt = null,
            RetryCount = 5 // Exceeds max retries
        };

        _context.OutboxMessages.AddRange(processedMessage, pendingMessage1, pendingMessage2, exceededRetriesMessage);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetPendingAsync(100)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Id == pendingMessage1.Id);
        result.Should().Contain(m => m.Id == pendingMessage2.Id);
        result.Should().NotContain(m => m.Id == processedMessage.Id);
        result.Should().NotContain(m => m.Id == exceededRetriesMessage.Id);
    }

    [Fact]
    public async Task GetPendingAsync_RespectsBatchSize()
    {
        // Arrange
        for (int i = 0; i < 150; i++)
        {
            _context.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = "PaymentCompletedEvent",
                Payload = "{}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                ProcessedAt = null,
                RetryCount = 0
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetPendingAsync(100)).ToList();

        // Assert
        result.Should().HaveCount(100);
    }

    [Fact]
    public async Task GetPendingAsync_OrdersByCreatedAt()
    {
        // Arrange
        var message1 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ProcessedAt = null,
            RetryCount = 0
        };

        var message2 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ProcessedAt = null,
            RetryCount = 0
        };

        var message3 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ProcessedAt = null,
            RetryCount = 0
        };

        _context.OutboxMessages.AddRange(message1, message2, message3);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _repository.GetPendingAsync(100)).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(message1.Id);
        result[1].Id.Should().Be(message2.Id);
        result[2].Id.Should().Be(message3.Id);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_SetsProcessedAt()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null,
            RetryCount = 0
        };

        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync();

        // Act
        await _repository.MarkAsProcessedAsync(message.Id);

        // Assert
        var updatedMessage = await _context.OutboxMessages.FindAsync(message.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.ProcessedAt.Should().NotBeNull();
        updatedMessage.Error.Should().BeNull();
    }

    [Fact]
    public async Task MarkAsFailedAsync_IncrementsRetryCount()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null,
            RetryCount = 2
        };

        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync();

        // Act
        await _repository.MarkAsFailedAsync(message.Id, "Test error");

        // Assert
        var updatedMessage = await _context.OutboxMessages.FindAsync(message.Id);
        updatedMessage.Should().NotBeNull();
        updatedMessage!.RetryCount.Should().Be(3);
        updatedMessage.Error.Should().Be("Test error");
    }

    [Fact]
    public async Task AddAsync_AddsMessage()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCompletedEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null,
            RetryCount = 0
        };

        // Act
        await _repository.AddAsync(message);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _context.OutboxMessages.FindAsync(message.Id);
        result.Should().NotBeNull();
        result!.EventType.Should().Be("PaymentCompletedEvent");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

