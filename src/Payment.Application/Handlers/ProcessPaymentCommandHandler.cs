using MediatR;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Mappings;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;

namespace Payment.Application.Handlers;

public sealed class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, PaymentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentStateService _stateService;

    public ProcessPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IPaymentStateService stateService)
    {
        _unitOfWork = unitOfWork;
        _stateService = stateService;
    }

    public async Task<PaymentDto> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment == null)
        {
            throw new KeyNotFoundException($"Payment with ID {request.PaymentId} not found");
        }

        payment.Process(request.TransactionId, _stateService);
        await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return payment.ToDto();
    }
}

