using Payment.Application.DTOs;
using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Services;

/// <summary>
/// Service interface for security incident response operations.
/// Follows Interface Segregation Principle - focused on security incident response only.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public interface ISecurityIncidentResponseService
{
    /// <summary>
    /// Assesses a security incident and provides recommendations.
    /// </summary>
    /// <param name="securityEvent">The security event to assess.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An assessment of the security incident with recommended actions.</returns>
    Task<SecurityIncidentAssessment> AssessIncidentAsync(
        SecurityEvent securityEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Contains a security incident using the specified strategy.
    /// </summary>
    /// <param name="incidentId">The security incident ID.</param>
    /// <param name="strategy">The containment strategy to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ContainIncidentAsync(
        SecurityIncidentId incidentId,
        ContainmentStrategy strategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an incident report for a security incident.
    /// </summary>
    /// <param name="incidentId">The security incident ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The incident report as a string (JSON or formatted text).</returns>
    Task<string> GenerateIncidentReportAsync(
        SecurityIncidentId incidentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes credentials based on a revocation request.
    /// </summary>
    /// <param name="request">The credential revocation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeCredentialsAsync(
        CredentialRevocationRequest request,
        CancellationToken cancellationToken = default);
}

