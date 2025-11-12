namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for validating webhook/callback signatures from payment providers.
/// Follows Interface Segregation Principle - separates signature validation concerns.
/// </summary>
public interface ICallbackSignatureValidator
{
    /// <summary>
    /// Validates a webhook signature for a specific payment provider.
    /// </summary>
    /// <param name="provider">Payment provider name (e.g., "ZainCash", "FIB", "Telr")</param>
    /// <param name="payload">Raw request body payload</param>
    /// <param name="signature">Signature from request header</param>
    /// <param name="timestamp">Timestamp from request header (for replay attack prevention)</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    Task<bool> ValidateAsync(string provider, string payload, string? signature, string? timestamp);
}

