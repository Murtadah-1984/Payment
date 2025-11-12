using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Commands;

public sealed record CompletePaymentCommand(
    Guid PaymentId,
    string? SettlementCurrency = null) : IRequest<PaymentDto>;

