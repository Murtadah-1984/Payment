using MediatR;
using Payment.Application.DTOs;
using Payment.Application.Mappings;
using Payment.Application.Queries;
using Payment.Domain.Interfaces;

namespace Payment.Application.Handlers;

public sealed class GetPaymentByOrderIdQueryHandler : IRequestHandler<GetPaymentByOrderIdQuery, PaymentDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPaymentByOrderIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaymentDto?> Handle(GetPaymentByOrderIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await _unitOfWork.Payments.GetByOrderIdAsync(request.OrderId, cancellationToken);
        return payment?.ToDto();
    }
}

