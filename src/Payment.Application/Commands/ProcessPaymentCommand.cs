using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Commands;

public sealed record ProcessPaymentCommand(
    Guid PaymentId,
    string TransactionId) : IRequest<PaymentDto>;

