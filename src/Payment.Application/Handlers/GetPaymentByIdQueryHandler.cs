using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Application.Mappings;
using Payment.Application.Queries;
using Payment.Domain.Common;
using Payment.Domain.Interfaces;

namespace Payment.Application.Handlers;

/// <summary>
/// Handler for getting payment by ID with caching support (Caching Strategy #9).
/// Uses Result pattern for error handling (Result Pattern #16) and OpenTelemetry tracing (Observability #15).
/// </summary>
public sealed class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, Result<PaymentDto>>
{
    private static readonly ActivitySource ActivitySource = new("Payment.Application");
    
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<GetPaymentByIdQueryHandler> _logger;

    public GetPaymentByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ICacheService cache,
        ILogger<GetPaymentByIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<PaymentDto>> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("GetPaymentById");
        activity?.SetTag("payment.id", request.PaymentId.ToString());

        try
        {
            var cacheKey = $"payment:{request.PaymentId}";

            // Try cache first (Caching Strategy #9)
            var cached = await _cache.GetAsync<PaymentDto>(cacheKey, cancellationToken);
            if (cached != null)
            {
                _logger.LogDebug("Cache hit for payment {PaymentId}", request.PaymentId);
                activity?.SetTag("cache.hit", true);
                activity?.SetTag("payment.status", cached.Status);
                return Result<PaymentDto>.Success(cached);
            }

            activity?.SetTag("cache.hit", false);

            // Fetch from database
            var payment = await _unitOfWork.Payments.GetByIdAsync(request.PaymentId, cancellationToken);
            if (payment == null)
            {
                _logger.LogWarning("Payment {PaymentId} not found", request.PaymentId);
                activity?.SetTag("payment.found", false);
                activity?.SetStatus(ActivityStatusCode.Error, "Payment not found");
                return Result<PaymentDto>.Failure(ErrorCodes.PaymentNotFound, 
                    $"Payment with ID {request.PaymentId} not found");
            }

            var dto = payment.ToDto();

            // Cache for 5 minutes
            await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);

            activity?.SetTag("payment.found", true);
            activity?.SetTag("payment.status", dto.Status);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return Result<PaymentDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment {PaymentId}", request.PaymentId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            return Result<PaymentDto>.Failure(ErrorCodes.InternalError, 
                $"An error occurred while retrieving payment: {ex.Message}");
        }
    }
}

