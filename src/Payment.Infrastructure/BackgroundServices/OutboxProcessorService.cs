using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using System.Text.Json;

namespace Payment.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes outbox messages for reliable event publishing.
/// Implements the Outbox pattern to ensure domain events are published reliably.
/// </summary>
public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;
    private const int MaxRetries = 5;

    public OutboxProcessorService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Outbox processor service stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var pendingMessages = (await unitOfWork.OutboxMessages.GetPendingAsync(BatchSize, cancellationToken)).ToList();

        if (!pendingMessages.Any())
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox messages", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                // Deserialize the event payload
                var eventType = Type.GetType($"Payment.Domain.Events.{message.EventType}");
                if (eventType == null)
                {
                    _logger.LogWarning("Unknown event type: {EventType}", message.EventType);
                    await unitOfWork.OutboxMessages.MarkAsFailedAsync(
                        message.Id,
                        $"Unknown event type: {message.EventType}",
                        cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType);
                if (domainEvent == null)
                {
                    _logger.LogWarning("Failed to deserialize event: {EventType}", message.EventType);
                    await unitOfWork.OutboxMessages.MarkAsFailedAsync(
                        message.Id,
                        "Failed to deserialize event",
                        cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    continue;
                }

                // Publish the event
                var topic = message.Topic ?? "payment.events";
                await eventPublisher.PublishAsync(topic, domainEvent, cancellationToken);

                // Mark as processed
                await unitOfWork.OutboxMessages.MarkAsProcessedAsync(message.Id, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Successfully processed outbox message {MessageId}", message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);

                // Mark as failed and increment retry count
                await unitOfWork.OutboxMessages.MarkAsFailedAsync(
                    message.Id,
                    ex.Message,
                    cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                // If max retries exceeded, log error (could move to dead letter queue)
                if (message.RetryCount >= MaxRetries)
                {
                    _logger.LogError(
                        "Outbox message {MessageId} exceeded max retries ({MaxRetries}). Moving to dead letter queue.",
                        message.Id,
                        MaxRetries);
                }
            }
        }
    }
}

