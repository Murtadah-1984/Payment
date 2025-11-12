namespace Payment.Domain.Interfaces;

/// <summary>
/// Repository interface for outbox messages.
/// Follows Interface Segregation Principle - focused on outbox operations only.
/// </summary>
public interface IOutboxMessageRepository
{
    /// <summary>
    /// Gets pending outbox messages that haven't been processed yet.
    /// </summary>
    Task<IEnumerable<Entities.OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new outbox message.
    /// </summary>
    Task AddAsync(Entities.OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an outbox message (e.g., after processing).
    /// </summary>
    Task UpdateAsync(Entities.OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as processed.
    /// </summary>
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as failed with error details.
    /// </summary>
    Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
}

