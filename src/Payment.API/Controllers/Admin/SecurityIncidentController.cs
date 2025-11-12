using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;

namespace Payment.API.Controllers.Admin;

/// <summary>
/// Admin API Controller for security incident management.
/// Follows Clean Architecture - thin controller that delegates to Application layer.
/// Requires SecurityAdminOnly authorization policy.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin/security/incidents")]
[ApiVersion("1.0")]
[Authorize(Policy = "SecurityAdminOnly")]
public class SecurityIncidentController : ControllerBase
{
    private readonly ISecurityIncidentResponseService _securityIncidentResponseService;
    private readonly ILogger<SecurityIncidentController> _logger;

    public SecurityIncidentController(
        ISecurityIncidentResponseService securityIncidentResponseService,
        ILogger<SecurityIncidentController> logger)
    {
        _securityIncidentResponseService = securityIncidentResponseService 
            ?? throw new ArgumentNullException(nameof(securityIncidentResponseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Assesses a security incident and provides recommendations.
    /// </summary>
    /// <param name="securityEvent">The security event to assess.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An assessment of the security incident with recommended actions.</returns>
    [HttpPost("assess")]
    [ProducesResponseType(typeof(SecurityIncidentAssessment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SecurityIncidentAssessment>> AssessIncident(
        [FromBody] SecurityEvent securityEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Assessing security incident. EventType: {EventType}, Resource: {Resource}",
            securityEvent.EventType, securityEvent.Resource);

        try
        {
            var assessment = await _securityIncidentResponseService.AssessIncidentAsync(
                securityEvent, 
                cancellationToken);

            return Ok(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing security incident");
            return BadRequest(new { error = "Failed to assess security incident", message = ex.Message });
        }
    }

    /// <summary>
    /// Contains a security incident using the specified strategy.
    /// </summary>
    /// <param name="incidentId">The security incident ID.</param>
    /// <param name="request">The containment request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("{incidentId}/contain")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ContainIncident(
        [FromRoute] Guid incidentId,
        [FromBody] ContainmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Containing security incident. IncidentId: {IncidentId}, Strategy: {Strategy}",
            incidentId, request.Strategy);

        try
        {
            var securityIncidentId = SecurityIncidentId.FromGuid(incidentId);
            await _securityIncidentResponseService.ContainIncidentAsync(
                securityIncidentId,
                request.Strategy,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Security incident not found: {IncidentId}", incidentId);
            return NotFound(new { error = "Security incident not found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error containing security incident: {IncidentId}", incidentId);
            return BadRequest(new { error = "Failed to contain security incident", message = ex.Message });
        }
    }

    /// <summary>
    /// Generates an incident report for a security incident.
    /// </summary>
    /// <param name="incidentId">The security incident ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The incident report as JSON.</returns>
    [HttpGet("{incidentId}/report")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string>> GetIncidentReport(
        [FromRoute] Guid incidentId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating incident report for incident: {IncidentId}", incidentId);

        try
        {
            var securityIncidentId = SecurityIncidentId.FromGuid(incidentId);
            var report = await _securityIncidentResponseService.GenerateIncidentReportAsync(
                securityIncidentId,
                cancellationToken);

            return Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Security incident not found: {IncidentId}", incidentId);
            return NotFound(new { error = "Security incident not found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating incident report: {IncidentId}", incidentId);
            return BadRequest(new { error = "Failed to generate incident report", message = ex.Message });
        }
    }

    /// <summary>
    /// Revokes credentials based on a revocation request.
    /// </summary>
    /// <param name="request">The credential revocation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("credentials/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeCredentials(
        [FromBody] CredentialRevocationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Revoking credentials. CredentialId: {CredentialId}, Type: {CredentialType}",
            request.CredentialId, request.CredentialType);

        try
        {
            await _securityIncidentResponseService.RevokeCredentialsAsync(request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking credentials: {CredentialId}", request.CredentialId);
            return BadRequest(new { error = "Failed to revoke credentials", message = ex.Message });
        }
    }
}

