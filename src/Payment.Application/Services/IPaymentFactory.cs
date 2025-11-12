using Payment.Application.DTOs;
using Payment.Domain.ValueObjects;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Factory responsible for creating Payment domain entities.
/// Follows Single Responsibility Principle - only handles entity creation.
/// </summary>
public interface IPaymentFactory
{
    /// <summary>
    /// Creates a Payment entity from a CreatePaymentDto and calculated split payment.
    /// </summary>
    PaymentEntity CreatePayment(
        CreatePaymentDto request,
        SplitPayment? splitPayment,
        Dictionary<string, string> metadata);
}

