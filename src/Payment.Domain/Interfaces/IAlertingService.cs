using Payment.Application.DTOs;
using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Service interface for sending automated alerts.
/// Follows Interface Segregation Principle - focused on alerting only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// Sends a generic alert.
    /// </summary>
    /// <param name="severity">The severity level of the alert.</param>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message.</param>
    /// <param name="metadata">Optional metadata dictionary.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAlertAsync(
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a payment failure alert.
    /// </summary>
    /// <param name="context">The payment failure context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendPaymentFailureAlertAsync(
        PaymentFailureContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a security incident alert.
    /// </summary>
    /// <param name="securityEvent">The security event.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendSecurityIncidentAlertAsync(
        SecurityEvent securityEvent,
        CancellationToken ct = default);

    /// <summary>
    /// Acknowledges an alert.
    /// </summary>
    /// <param name="alertKey">The alert key to acknowledge.</param>
    /// <param name="acknowledgedBy">The user who acknowledged the alert.</param>
    /// <param name="notes">Optional notes about the acknowledgment.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AcknowledgeAlertAsync(
        string alertKey,
        string acknowledgedBy,
        string? notes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if an alert has been acknowledged.
    /// </summary>
    /// <param name="alertKey">The alert key to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the alert has been acknowledged, false otherwise.</returns>
    Task<bool> IsAlertAcknowledgedAsync(
        string alertKey,
        CancellationToken ct = default);
}

