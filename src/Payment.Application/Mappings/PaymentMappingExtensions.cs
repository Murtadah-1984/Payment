using Payment.Application.DTOs;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Mappings;

public static class PaymentMappingExtensions
{
    public static PaymentDto ToDto(this PaymentEntity payment)
    {
        SplitPaymentDto? splitPaymentDto = null;
        if (payment.SplitPayment != null)
        {
            splitPaymentDto = new SplitPaymentDto(
                payment.SplitPayment.SystemShare,
                payment.SplitPayment.OwnerShare,
                payment.SplitPayment.SystemFeePercent);
        }

        CardTokenDto? cardTokenDto = null;
        if (payment.CardToken != null)
        {
            cardTokenDto = new CardTokenDto(
                payment.CardToken.Last4Digits,
                payment.CardToken.CardBrand);
        }

        SettlementDto? settlementDto = null;
        if (payment.SettlementCurrency != null && payment.SettlementAmount.HasValue && payment.ExchangeRate.HasValue && payment.SettledAt.HasValue)
        {
            settlementDto = new SettlementDto(
                payment.SettlementCurrency.Code,
                payment.SettlementAmount.Value,
                payment.ExchangeRate.Value,
                payment.SettledAt.Value);
        }

        ThreeDSecureStatusDto? threeDSecureDto = null;
        if (payment.ThreeDSecureStatus != Domain.Enums.ThreeDSecureStatus.NotRequired)
        {
            threeDSecureDto = new ThreeDSecureStatusDto(
                payment.ThreeDSecureStatus.ToString(),
                payment.ThreeDSecureCavv,
                payment.ThreeDSecureEci,
                payment.ThreeDSecureXid,
                payment.ThreeDSecureVersion);
        }

        return new PaymentDto(
            payment.Id.Value,
            payment.Amount.Value,
            payment.Currency.Code,
            payment.PaymentMethod.Value,
            payment.Provider.Name,
            payment.MerchantId,
            payment.OrderId,
            payment.Status.ToString(),
            payment.TransactionId,
            payment.FailureReason,
            splitPaymentDto,
            payment.Metadata.Count > 0 ? payment.Metadata : null,
            cardTokenDto,
            payment.CreatedAt,
            payment.UpdatedAt,
            settlementDto,
            threeDSecureDto);
    }
}

