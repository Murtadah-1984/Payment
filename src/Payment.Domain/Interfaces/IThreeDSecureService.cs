using Payment.Domain.Enums;
using Payment.Domain.ValueObjects;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for 3D Secure authentication service.
/// Follows Interface Segregation Principle - focused on 3DS operations only.
/// </summary>
public interface IThreeDSecureService
{
    /// <summary>
    /// Initiates a 3D Secure authentication flow for a payment.
    /// </summary>
    /// <param name="paymentId">The payment ID</param>
    /// <param name="amount">The payment amount</param>
    /// <param name="currency">The payment currency</param>
    /// <param name="cardToken">The tokenized card information</param>
    /// <param name="returnUrl">The URL to return to after authentication</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A 3DS challenge if authentication is required, or null if not required</returns>
    Task<ThreeDSecureChallenge?> InitiateAuthenticationAsync(
        Guid paymentId,
        Amount amount,
        Currency currency,
        CardToken cardToken,
        string returnUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a 3D Secure authentication flow using the response from the ACS.
    /// </summary>
    /// <param name="paymentId">The payment ID</param>
    /// <param name="pareq">The Payment Authentication Request (PAReq) that was sent</param>
    /// <param name="ares">The Authentication Response (ARes) from the ACS</param>
    /// <param name="md">The merchant data that was sent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The 3DS authentication result</returns>
    Task<ThreeDSecureResult> CompleteAuthenticationAsync(
        Guid paymentId,
        string pareq,
        string ares,
        string md,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if 3D Secure authentication is required for a payment.
    /// </summary>
    /// <param name="amount">The payment amount</param>
    /// <param name="currency">The payment currency</param>
    /// <param name="cardToken">The tokenized card information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if 3DS is required, false otherwise</returns>
    Task<bool> IsAuthenticationRequiredAsync(
        Amount amount,
        Currency currency,
        CardToken cardToken,
        CancellationToken cancellationToken = default);
}

