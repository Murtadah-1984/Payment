using Payment.Domain.ValueObjects;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Service interface for processing refunds.
/// Follows Interface Segregation Principle - focused on refund operations only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// Processes a refund for a payment.
    /// </summary>
    /// <param name="paymentId">The payment ID to refund.</param>
    /// <param name="reason">The reason for the refund.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the refund was successful, false otherwise.</returns>
    Task<bool> ProcessRefundAsync(PaymentId paymentId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes multiple refunds in batch.
    /// </summary>
    /// <param name="paymentIds">The payment IDs to refund.</param>
    /// <param name="reason">The reason for the refunds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping payment IDs to refund success status.</returns>
    Task<Dictionary<PaymentId, bool>> ProcessRefundsAsync(
        IEnumerable<PaymentId> paymentIds,
        string reason,
        CancellationToken cancellationToken = default);
}

