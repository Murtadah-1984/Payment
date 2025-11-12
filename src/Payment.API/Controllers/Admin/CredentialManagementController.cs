using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;

namespace Payment.API.Controllers.Admin;

/// <summary>
/// Admin API Controller for credential management.
/// Follows Clean Architecture - thin controller that delegates to Domain layer.
/// Requires AdminOnly authorization policy.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/admin/credentials")]
[ApiVersion("1.0")]
[Authorize(Policy = "AdminOnly")]
public class CredentialManagementController : ControllerBase
{
    private readonly ICredentialRevocationService _revocationService;
    private readonly ILogger<CredentialManagementController> _logger;

    public CredentialManagementController(
        ICredentialRevocationService revocationService,
        ILogger<CredentialManagementController> logger)
    {
        _revocationService = revocationService ?? throw new ArgumentNullException(nameof(revocationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    /// <param name="apiKeyId">The API key ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("api-keys/{apiKeyId}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeApiKey(
        [FromRoute] string apiKeyId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Revoking API key. ApiKeyId: {ApiKeyId}", apiKeyId);

        try
        {
            await _revocationService.RevokeApiKeyAsync(apiKeyId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid API key ID: {ApiKeyId}", apiKeyId);
            return BadRequest(new { error = "Invalid API key ID", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking API key: {ApiKeyId}", apiKeyId);
            return BadRequest(new { error = "Failed to revoke API key", message = ex.Message });
        }
    }

    /// <summary>
    /// Revokes a JWT token.
    /// </summary>
    /// <param name="tokenId">The token ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("jwt-tokens/{tokenId}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeJwtToken(
        [FromRoute] string tokenId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Revoking JWT token. TokenId: {TokenId}", tokenId);

        try
        {
            await _revocationService.RevokeJwtTokenAsync(tokenId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid token ID: {TokenId}", tokenId);
            return BadRequest(new { error = "Invalid token ID", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking JWT token: {TokenId}", tokenId);
            return BadRequest(new { error = "Failed to revoke JWT token", message = ex.Message });
        }
    }

    /// <summary>
    /// Rotates a secret (database connection, payment provider key, etc.).
    /// </summary>
    /// <param name="secretName">The name of the secret to rotate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("secrets/{secretName}/rotate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RotateSecret(
        [FromRoute] string secretName,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rotating secret. SecretName: {SecretName}", secretName);

        try
        {
            await _revocationService.RotateSecretsAsync(secretName, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid secret name: {SecretName}", secretName);
            return BadRequest(new { error = "Invalid secret name", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating secret: {SecretName}", secretName);
            return BadRequest(new { error = "Failed to rotate secret", message = ex.Message });
        }
    }

    /// <summary>
    /// Checks if a credential is revoked.
    /// </summary>
    /// <param name="credentialId">The credential ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if revoked, false otherwise.</returns>
    [HttpGet("check/{credentialId}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<bool>> IsRevoked(
        [FromRoute] string credentialId,
        CancellationToken cancellationToken)
    {
        try
        {
            var isRevoked = await _revocationService.IsRevokedAsync(credentialId, cancellationToken);
            return Ok(isRevoked);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid credential ID: {CredentialId}", credentialId);
            return BadRequest(new { error = "Invalid credential ID", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking credential revocation: {CredentialId}", credentialId);
            return BadRequest(new { error = "Failed to check credential revocation", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets all revoked credentials, optionally filtered by date.
    /// </summary>
    /// <param name="since">Optional date filter - only return credentials revoked after this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of revoked credentials.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RevokedCredential>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<RevokedCredential>>> GetRevokedCredentials(
        [FromQuery] DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting revoked credentials. Since: {Since}", since);

        try
        {
            var revokedCredentials = await _revocationService.GetRevokedCredentialsAsync(since, cancellationToken);
            return Ok(revokedCredentials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revoked credentials");
            return BadRequest(new { error = "Failed to get revoked credentials", message = ex.Message });
        }
    }
}

