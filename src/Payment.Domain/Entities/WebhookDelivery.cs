namespace Payment.Domain.Entities;

/// <summary>
/// Entity representing a webhook delivery attempt to an external system.
/// Tracks delivery status, retry attempts, and exponential backoff timing.
/// </summary>
public class WebhookDelivery : Entity
{
    private WebhookDelivery() { } // EF Core

    public WebhookDelivery(
        Guid id,
        Guid paymentId,
        string webhookUrl,
        string eventType,
        string payload,
        int maxRetries = 5,
        TimeSpan? initialRetryDelay = null)
    {
        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment ID cannot be empty", nameof(paymentId));

        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("Webhook URL cannot be null or empty", nameof(webhookUrl));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));

        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be null or empty", nameof(payload));

        if (maxRetries < 0)
            throw new ArgumentException("Max retries cannot be negative", nameof(maxRetries));

        Id = id;
        PaymentId = paymentId;
        WebhookUrl = webhookUrl;
        EventType = eventType;
        Payload = payload;
        MaxRetries = maxRetries;
        RetryCount = 0;
        Status = WebhookDeliveryStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        NextRetryAt = DateTime.UtcNow;
        InitialRetryDelay = initialRetryDelay ?? TimeSpan.FromSeconds(1);
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public string WebhookUrl { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public WebhookDeliveryStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public DateTime? LastAttemptedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public string? LastError { get; private set; }
    public int? LastHttpStatusCode { get; private set; }
    public TimeSpan InitialRetryDelay { get; private set; }

    /// <summary>
    /// Marks the webhook as successfully delivered.
    /// </summary>
    public void MarkAsDelivered()
    {
        Status = WebhookDeliveryStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        LastAttemptedAt = DateTime.UtcNow;
        LastError = null;
    }

    /// <summary>
    /// Marks the webhook delivery as failed and schedules the next retry with exponential backoff.
    /// </summary>
    /// <param name="error">Error message from the failed attempt</param>
    /// <param name="httpStatusCode">HTTP status code if available</param>
    public void MarkAsFailed(string error, int? httpStatusCode = null)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Error message cannot be null or empty", nameof(error));

        LastError = error;
        LastHttpStatusCode = httpStatusCode;
        LastAttemptedAt = DateTime.UtcNow;
        RetryCount++;

        if (RetryCount >= MaxRetries)
        {
            Status = WebhookDeliveryStatus.Failed;
            NextRetryAt = null;
        }
        else
        {
            Status = WebhookDeliveryStatus.Pending;
            // Exponential backoff: delay = initialDelay * 2^retryCount
            var delay = TimeSpan.FromMilliseconds(
                InitialRetryDelay.TotalMilliseconds * Math.Pow(2, RetryCount - 1));
            
            // Cap maximum delay at 1 hour
            if (delay > TimeSpan.FromHours(1))
                delay = TimeSpan.FromHours(1);

            NextRetryAt = DateTime.UtcNow.Add(delay);
        }
    }

    /// <summary>
    /// Checks if the webhook is ready for retry.
    /// </summary>
    public bool IsReadyForRetry => 
        Status == WebhookDeliveryStatus.Pending && 
        NextRetryAt.HasValue && 
        DateTime.UtcNow >= NextRetryAt.Value &&
        RetryCount < MaxRetries;

    /// <summary>
    /// Checks if the webhook has exhausted all retry attempts.
    /// </summary>
    public bool IsExhausted => Status == WebhookDeliveryStatus.Failed || RetryCount >= MaxRetries;
}

/// <summary>
/// Status of a webhook delivery attempt.
/// </summary>
public enum WebhookDeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2
}

