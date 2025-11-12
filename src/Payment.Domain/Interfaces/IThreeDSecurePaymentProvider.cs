using Payment.Domain.ValueObjects;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for payment providers that support 3D Secure authentication.
/// Follows Interface Segregation Principle - separates 3DS concerns from basic payment processing.
/// Providers that support 3DS should implement this interface in addition to IPaymentProvider.
/// </summary>
public interface IThreeDSecurePaymentProvider : IPaymentProvider
{
    /// <summary>
    /// Initiates a 3D Secure authentication flow for a payment.
    /// This method is called when a payment requires 3DS authentication.
    /// </summary>
    /// <param name="request">The payment request containing payment details</param>
    /// <param name="returnUrl">The URL to return to after 3DS authentication is complete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A 3DS challenge if authentication is required, or null if not required</returns>
    Task<ThreeDSecureChallenge?> InitiateThreeDSecureAsync(
        PaymentRequest request,
        string returnUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a 3D Secure authentication flow using the response from the ACS.
    /// This method is called after the user completes authentication on the ACS.
    /// </summary>
    /// <param name="request">The original payment request</param>
    /// <param name="pareq">The Payment Authentication Request (PAReq) that was sent</param>
    /// <param name="ares">The Authentication Response (ARes) from the ACS</param>
    /// <param name="md">The merchant data that was sent</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The 3DS authentication result and updated payment result</returns>
    Task<ThreeDSecurePaymentResult> CompleteThreeDSecureAsync(
        PaymentRequest request,
        string pareq,
        string ares,
        string md,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if 3D Secure authentication is required for a payment.
    /// This method allows providers to determine if 3DS should be triggered based on their own rules.
    /// </summary>
    /// <param name="request">The payment request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if 3DS is required, false otherwise</returns>
    Task<bool> IsThreeDSecureRequiredAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a 3D Secure authentication flow that includes both 3DS result and payment result.
/// </summary>
public sealed record ThreeDSecurePaymentResult(
    ThreeDSecureResult ThreeDSecureResult,
    PaymentResult PaymentResult);

