using Payment.Application.DTOs;

namespace Payment.Application.Services;

public interface IPaymentOrchestrator
{
    Task<PaymentDto> ProcessPaymentAsync(CreatePaymentDto request, CancellationToken cancellationToken = default);
}

