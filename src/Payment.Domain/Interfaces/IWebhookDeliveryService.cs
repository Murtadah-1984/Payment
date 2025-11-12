namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for delivering webhooks to external systems with retry mechanism.
/// Follows Interface Segregation Principle - focused on webhook delivery only.
/// </summary>
public interface IWebhookDeliveryService
{
    /// <summary>
    /// Sends a webhook to the specified URL with the given payload.
    /// </summary>
    /// <param name="webhookUrl">The URL to send the webhook to</param>
    /// <param name="eventType">The type of event (e.g., "payment.completed", "payment.failed")</param>
    /// <param name="payload">The JSON payload to send</param>
    /// <param name="headers">Optional custom headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the webhook was successfully delivered, false otherwise</returns>
    Task<WebhookDeliveryResult> SendWebhookAsync(
        string webhookUrl,
        string eventType,
        string payload,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a webhook for delivery with automatic retry on failure.
    /// The webhook will be persisted and retried by a background service.
    /// </summary>
    /// <param name="paymentId">The payment ID associated with this webhook</param>
    /// <param name="webhookUrl">The URL to send the webhook to</param>
    /// <param name="eventType">The type of event</param>
    /// <param name="payload">The JSON payload to send</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created webhook delivery record</returns>
    Task<Guid> ScheduleWebhookAsync(
        Guid paymentId,
        string webhookUrl,
        string eventType,
        string payload,
        int maxRetries = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a webhook delivery attempt.
/// </summary>
public record WebhookDeliveryResult(
    bool Success,
    int? HttpStatusCode = null,
    string? ErrorMessage = null,
    TimeSpan? ResponseTime = null);

