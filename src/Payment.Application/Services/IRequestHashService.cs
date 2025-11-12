using Payment.Application.DTOs;

namespace Payment.Application.Services;

/// <summary>
/// Service for computing request hashes for idempotency validation.
/// Follows Single Responsibility Principle - only handles hash computation.
/// </summary>
public interface IRequestHashService
{
    /// <summary>
    /// Computes a SHA-256 hash of the request data in canonical JSON format.
    /// This ensures that the same request data always produces the same hash.
    /// </summary>
    /// <param name="request">The payment creation request</param>
    /// <returns>SHA-256 hash as hexadecimal string</returns>
    string ComputeRequestHash(CreatePaymentDto request);
}


