using Payment.Application.DTOs;
using Payment.Domain.ValueObjects;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Implementation of payment factory.
/// Single Responsibility: Only creates Payment domain entities.
/// </summary>
public class PaymentFactory : IPaymentFactory
{
    public PaymentEntity CreatePayment(
        CreatePaymentDto request,
        SplitPayment? splitPayment,
        Dictionary<string, string> metadata)
    {
        return new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(request.Amount),
            Currency.FromCode(request.Currency),
            PaymentMethod.FromString(request.PaymentMethod),
            PaymentProvider.FromString(request.Provider),
            request.MerchantId,
            request.OrderId,
            splitPayment,
            metadata);
    }
}

