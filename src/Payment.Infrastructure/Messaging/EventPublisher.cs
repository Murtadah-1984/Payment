using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using System.Text.Json;
using System.Text;

namespace Payment.Infrastructure.Messaging;

/// <summary>
/// Event publisher implementation for message bus integration.
/// Supports RabbitMQ, Kafka, Azure Service Bus, etc.
/// This is a simplified implementation - in production, use a proper message bus client.
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IConfiguration _configuration;

    public EventPublisher(ILogger<EventPublisher> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task PublishAsync<T>(string topic, T @event, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            _logger.LogInformation("Publishing event to topic {Topic}, EventType: {EventType}", topic, typeof(T).Name);

            // Serialize event to JSON
            var json = JsonSerializer.Serialize(@event, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonSerializerDefaults.Web.PropertyNamingPolicy
            });

            // In production, this would publish to RabbitMQ/Kafka/Azure Service Bus
            // For now, we'll log it and simulate async operation
            _logger.LogInformation("Event published to {Topic}: {EventJson}", topic, json);

            // TODO: Integrate with actual message bus
            // Example for RabbitMQ:
            // await _rabbitMqChannel.BasicPublishAsync(
            //     exchange: "payment.events",
            //     routingKey: topic,
            //     body: Encoding.UTF8.GetBytes(json),
            //     cancellationToken: cancellationToken);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event to topic {Topic}", topic);
            throw;
        }
    }

    public async Task PublishWithRetryAsync<T>(string topic, T @event, int maxRetries = 3, CancellationToken cancellationToken = default) where T : class
    {
        var retryCount = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (retryCount < maxRetries)
        {
            try
            {
                await PublishAsync(topic, @event, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Failed to publish event after {RetryCount} retries", maxRetries);
                    throw;
                }

                _logger.LogWarning(ex, "Failed to publish event, retrying in {Delay}ms (attempt {RetryCount}/{MaxRetries})", 
                    delay.TotalMilliseconds, retryCount, maxRetries);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }
    }
}

