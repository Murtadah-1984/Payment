using Payment.Domain.Entities;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Repository interface for webhook delivery entities.
/// </summary>
public interface IWebhookDeliveryRepository : IRepository<WebhookDelivery>
{
    /// <summary>
    /// Gets all webhook deliveries that are ready for retry.
    /// </summary>
    Task<IEnumerable<WebhookDelivery>> GetPendingRetriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all webhook deliveries for a specific payment.
    /// </summary>
    Task<IEnumerable<WebhookDelivery>> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets webhook deliveries that have failed and exhausted all retries.
    /// </summary>
    Task<IEnumerable<WebhookDelivery>> GetFailedDeliveriesAsync(CancellationToken cancellationToken = default);
}

