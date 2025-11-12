using Payment.Application.DTOs;
using Payment.Domain.Interfaces;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Implementation of idempotency service.
/// Single Responsibility: Only checks for existing payments to ensure idempotency.
/// </summary>
public class IdempotencyService : IIdempotencyService
{
    private readonly IUnitOfWork _unitOfWork;

    public IdempotencyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaymentEntity?> CheckExistingPaymentAsync(CreatePaymentDto request, CancellationToken cancellationToken = default)
    {
        // Check by OrderId first (most common idempotency key)
        var existingByOrderId = await _unitOfWork.Payments.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existingByOrderId != null)
        {
            return existingByOrderId;
        }

        // Optionally check by RequestId if stored in metadata
        // This would require a query by metadata, which might need indexing
        // For now, OrderId is sufficient for most use cases

        return null;
    }
}

