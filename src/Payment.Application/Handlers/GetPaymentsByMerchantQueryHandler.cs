using MediatR;
using Payment.Application.DTOs;
using Payment.Application.Mappings;
using Payment.Application.Queries;
using Payment.Domain.Interfaces;

namespace Payment.Application.Handlers;

public sealed class GetPaymentsByMerchantQueryHandler : IRequestHandler<GetPaymentsByMerchantQuery, IEnumerable<PaymentDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPaymentsByMerchantQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<PaymentDto>> Handle(GetPaymentsByMerchantQuery request, CancellationToken cancellationToken)
    {
        var payments = await _unitOfWork.Payments.GetByMerchantIdAsync(request.MerchantId, cancellationToken);
        return payments.Select(p => p.ToDto());
    }
}

