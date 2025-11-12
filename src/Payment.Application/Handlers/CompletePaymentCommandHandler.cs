using MediatR;
using Microsoft.Extensions.Configuration;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Mappings;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;

namespace Payment.Application.Handlers;

public sealed class CompletePaymentCommandHandler : IRequestHandler<CompletePaymentCommand, PaymentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentStateService _stateService;
    private readonly ISettlementService? _settlementService;
    private readonly IConfiguration _configuration;

    public CompletePaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IPaymentStateService stateService,
        ISettlementService? settlementService = null,
        IConfiguration? configuration = null)
    {
        _unitOfWork = unitOfWork;
        _stateService = stateService;
        _settlementService = settlementService;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<PaymentDto> Handle(CompletePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment == null)
        {
            throw new KeyNotFoundException($"Payment with ID {request.PaymentId} not found");
        }

        payment.Complete(_stateService);

        // Process multi-currency settlement if enabled (Multi-Currency Settlement #21)
        if (_settlementService != null)
        {
            var settlementCurrency = request.SettlementCurrency 
                ?? _configuration["Settlement:Currency"] 
                ?? "USD";
            
            await _settlementService.ProcessSettlementAsync(payment, settlementCurrency, cancellationToken);
        }

        await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return payment.ToDto();
    }
}

