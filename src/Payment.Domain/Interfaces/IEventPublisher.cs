namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for publishing domain events to message bus.
/// Follows Interface Segregation Principle - focused on event publishing only.
/// Supports RabbitMQ, Kafka, Azure Service Bus, etc.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a domain event to the specified topic/exchange.
    /// </summary>
    /// <param name="topic">Topic/exchange name (e.g., "payment.reports.monthly.generated")</param>
    /// <param name="event">Domain event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task PublishAsync<T>(string topic, T @event, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes a domain event with retry policy (exponential backoff).
    /// </summary>
    /// <param name="topic">Topic/exchange name</param>
    /// <param name="event">Domain event to publish</param>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task PublishWithRetryAsync<T>(string topic, T @event, int maxRetries = 3, CancellationToken cancellationToken = default) where T : class;
}

