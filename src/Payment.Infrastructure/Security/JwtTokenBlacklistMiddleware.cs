using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace Payment.Infrastructure.Security;

/// <summary>
/// Middleware to check JWT tokens against the revocation blacklist.
/// Follows Single Responsibility Principle - only handles JWT blacklist checking.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class JwtTokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtTokenBlacklistMiddleware> _logger;

    public JwtTokenBlacklistMiddleware(
        RequestDelegate next,
        ILogger<JwtTokenBlacklistMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICredentialRevocationService revocationService)
    {
        if (revocationService == null)
            throw new ArgumentNullException(nameof(revocationService));

        // Extract JWT token from Authorization header
        var token = ExtractTokenFromHeader(context);

        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                // Extract token ID (JTI claim) from JWT
                var tokenId = ExtractTokenId(token);

                if (!string.IsNullOrEmpty(tokenId))
                {
                    // Check if token is revoked
                    var isRevoked = await revocationService.IsRevokedAsync(tokenId, context.RequestAborted);

                    if (isRevoked)
                    {
                        _logger.LogWarning(
                            "Revoked JWT token detected. TokenId: {TokenId}, Path: {Path}",
                            tokenId, context.Request.Path);

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Token has been revoked", context.RequestAborted);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking JWT token blacklist");
                // Continue processing - don't block requests if blacklist check fails
            }
        }

        await _next(context);
    }

    private string? ExtractTokenFromHeader(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return authHeader.Substring("Bearer ".Length).Trim();
    }

    private string? ExtractTokenId(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return null;

            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Id; // JTI claim
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract token ID from JWT");
            return null;
        }
    }
}

