using Payment.Domain.Interfaces;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Service responsible for updating payment status based on processing results.
/// Follows Single Responsibility Principle - only handles status updates.
/// </summary>
public interface IPaymentStatusUpdater
{
    /// <summary>
    /// Updates payment status based on processing result.
    /// </summary>
    void UpdatePaymentStatus(PaymentEntity payment, PaymentResult result);
}

