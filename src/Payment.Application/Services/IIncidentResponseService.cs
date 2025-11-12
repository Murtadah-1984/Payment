using Payment.Application.DTOs;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Services;

/// <summary>
/// Service interface for incident response related to payment failures.
/// Follows Interface Segregation Principle - focused on incident response only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface IIncidentResponseService
{
    /// <summary>
    /// Assesses a payment failure incident and provides recommendations.
    /// </summary>
    /// <param name="context">The payment failure context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An assessment of the incident with recommended actions.</returns>
    Task<IncidentAssessment> AssessPaymentFailureAsync(
        PaymentFailureContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies stakeholders about an incident.
    /// </summary>
    /// <param name="severity">The severity of the incident.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if notification was sent successfully.</returns>
    Task<bool> NotifyStakeholdersAsync(
        IncidentSeverity severity,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes automatic refunds for a list of payment IDs.
    /// </summary>
    /// <param name="paymentIds">The payment IDs to refund.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping payment IDs to refund success status.</returns>
    Task<Dictionary<PaymentId, bool>> ProcessAutomaticRefundsAsync(
        IEnumerable<PaymentId> paymentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets incident metrics for a specified time range.
    /// </summary>
    /// <param name="timeRange">The time range to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Incident metrics for the time range.</returns>
    Task<IncidentMetricsDto> GetIncidentMetricsAsync(
        TimeRange timeRange,
        CancellationToken cancellationToken = default);
}

