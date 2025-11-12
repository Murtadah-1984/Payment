namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for payment providers that support callback/webhook verification.
/// Follows Interface Segregation Principle - separates callback concerns from payment processing.
/// </summary>
public interface IPaymentCallbackProvider
{
    /// <summary>
    /// Verifies a payment callback/webhook from the provider.
    /// </summary>
    /// <param name="callbackData">Provider-specific callback data (token, payment ID, order ID, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment result with verification status</returns>
    Task<PaymentResult> VerifyCallbackAsync(
        Dictionary<string, string> callbackData,
        CancellationToken cancellationToken = default);
}

