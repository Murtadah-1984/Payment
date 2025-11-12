using Payment.API.GraphQL.Types;
using Payment.Application.DTOs;

namespace Payment.API.GraphQL.Mappings;

/// <summary>
/// Mapping extensions for converting between Application DTOs and GraphQL types.
/// Follows Clean Architecture - Presentation layer maps Application layer DTOs.
/// </summary>
public static class PaymentGraphQLMappings
{
    /// <summary>
    /// Maps PaymentDto to PaymentType.
    /// </summary>
    public static PaymentType ToGraphQLType(this PaymentDto dto)
    {
        return new PaymentType
        {
            Id = dto.Id,
            Amount = dto.Amount,
            Currency = dto.Currency,
            PaymentMethod = dto.PaymentMethod,
            Provider = dto.Provider,
            MerchantId = dto.MerchantId,
            OrderId = dto.OrderId,
            Status = dto.Status,
            TransactionId = dto.TransactionId,
            FailureReason = dto.FailureReason,
            SplitPayment = dto.SplitPayment?.ToGraphQLType(),
            Metadata = dto.Metadata,
            CardToken = dto.CardToken?.ToGraphQLType(),
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    /// <summary>
    /// Maps SplitPaymentDto to SplitPaymentType.
    /// </summary>
    public static SplitPaymentType ToGraphQLType(this SplitPaymentDto dto)
    {
        return new SplitPaymentType
        {
            SystemShare = dto.SystemShare,
            OwnerShare = dto.OwnerShare,
            SystemFeePercent = dto.SystemFeePercent
        };
    }

    /// <summary>
    /// Maps CardTokenDto to CardTokenType.
    /// </summary>
    public static CardTokenType ToGraphQLType(this CardTokenDto dto)
    {
        return new CardTokenType
        {
            Last4Digits = dto.Last4Digits,
            CardBrand = dto.CardBrand
        };
    }

    /// <summary>
    /// Maps CreatePaymentInput to CreatePaymentDto.
    /// </summary>
    public static CreatePaymentDto ToApplicationDto(this CreatePaymentInput input)
    {
        return new CreatePaymentDto(
            RequestId: input.RequestId,
            Amount: input.Amount,
            Currency: input.Currency,
            PaymentMethod: input.PaymentMethod,
            Provider: input.Provider,
            MerchantId: input.MerchantId,
            OrderId: input.OrderId,
            ProjectCode: input.ProjectCode,
            IdempotencyKey: input.IdempotencyKey,
            SystemFeePercent: input.SystemFeePercent,
            SplitRule: input.SplitRule?.ToApplicationDto(),
            Metadata: input.Metadata,
            CallbackUrl: input.CallbackUrl,
            CustomerEmail: input.CustomerEmail,
            CustomerPhone: input.CustomerPhone,
            NfcToken: input.NfcToken,
            DeviceId: input.DeviceId,
            CustomerId: input.CustomerId
        );
    }

    /// <summary>
    /// Maps SplitRuleInput to SplitRuleDto.
    /// </summary>
    public static SplitRuleDto ToApplicationDto(this SplitRuleInput input)
    {
        return new SplitRuleDto(
            SystemFeePercent: input.SystemFeePercent,
            Accounts: input.Accounts.Select(a => a.ToApplicationDto()).ToList()
        );
    }

    /// <summary>
    /// Maps SplitAccountInput to SplitAccountDto.
    /// </summary>
    public static SplitAccountDto ToApplicationDto(this SplitAccountInput input)
    {
        return new SplitAccountDto(
            AccountType: input.AccountType,
            AccountIdentifier: input.AccountIdentifier,
            Percentage: input.Percentage
        );
    }
}

