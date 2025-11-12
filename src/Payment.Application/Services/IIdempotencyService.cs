using Payment.Application.DTOs;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Service responsible for idempotency checking.
/// Follows Single Responsibility Principle - only handles idempotency logic.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if a payment already exists for the given request.
    /// Returns existing payment if found, null otherwise.
    /// </summary>
    Task<PaymentEntity?> CheckExistingPaymentAsync(CreatePaymentDto request, CancellationToken cancellationToken = default);
}

