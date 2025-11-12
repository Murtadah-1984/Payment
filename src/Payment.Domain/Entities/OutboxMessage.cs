namespace Payment.Domain.Entities;

/// <summary>
/// Outbox message entity for reliable event publishing.
/// Implements the Outbox pattern to ensure domain events are published reliably.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public string? Topic { get; set; }
}

