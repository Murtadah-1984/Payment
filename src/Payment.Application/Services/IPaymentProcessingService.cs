using Payment.Domain.Interfaces;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Service responsible for processing payments through payment providers.
/// Follows Single Responsibility Principle - only handles provider interaction.
/// </summary>
public interface IPaymentProcessingService
{
    /// <summary>
    /// Processes a payment through the appropriate payment provider.
    /// </summary>
    Task<PaymentResult> ProcessPaymentAsync(
        PaymentEntity payment,
        IPaymentProvider provider,
        CancellationToken cancellationToken = default);
}

