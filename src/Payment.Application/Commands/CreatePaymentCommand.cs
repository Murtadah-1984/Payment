using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Commands;

public sealed record CreatePaymentCommand(
    Guid RequestId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Provider,
    string MerchantId,
    string OrderId,
    string ProjectCode,
    string IdempotencyKey,
    decimal? SystemFeePercent = null,
    SplitRuleDto? SplitRule = null,
    Dictionary<string, string>? Metadata = null,
    string? CallbackUrl = null,
    string? CustomerEmail = null,
    string? CustomerPhone = null,
    string? NfcToken = null,              // Tokenized NFC payload from mobile SDK (Apple Pay, Google Pay, Tap SDK)
    string? DeviceId = null,              // Device or terminal ID for Tap-to-Pay transactions
    string? CustomerId = null) : IRequest<PaymentDto>; // Customer identifier for Tap-to-Pay transactions

