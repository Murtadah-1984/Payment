using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.Queries;

public sealed record GetPaymentsByMerchantQuery(string MerchantId) : IRequest<IEnumerable<PaymentDto>>;

