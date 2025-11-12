using MediatR;
using Payment.Application.DTOs;
using Payment.Domain.Common;

namespace Payment.Application.Commands;

public sealed record RefundPaymentCommand(
    Guid PaymentId,
    string RefundTransactionId) : IRequest<Result<PaymentDto>>;

