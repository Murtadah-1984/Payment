using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using Payment.Domain.Exceptions;

namespace Payment.Application.Handlers;

/// <summary>
/// Handler for creating a payment.
/// Uses OpenTelemetry tracing (Observability #15) for distributed tracing.
/// Note: Result pattern is handled by PaymentOrchestrator which may throw exceptions for exceptional cases.
/// </summary>
public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    private static readonly ActivitySource ActivitySource = new("Payment.Application");
    
    private readonly IPaymentOrchestrator _orchestrator;
    private readonly IFeatureManager _featureManager;
    private readonly IFraudDetectionService? _fraudDetectionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CreatePaymentCommandHandler> _logger;
    private readonly IMetricsRecorder? _metricsRecorder;

    public CreatePaymentCommandHandler(
        IPaymentOrchestrator orchestrator,
        IFeatureManager featureManager,
        ILogger<CreatePaymentCommandHandler> logger,
        IHttpContextAccessor httpContextAccessor,
        IFraudDetectionService? fraudDetectionService = null,
        IMetricsRecorder? metricsRecorder = null)
    {
        _orchestrator = orchestrator;
        _featureManager = featureManager;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _fraudDetectionService = fraudDetectionService;
        _metricsRecorder = metricsRecorder;
    }

    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("CreatePayment");
        activity?.SetTag("payment.order.id", request.OrderId);
        activity?.SetTag("payment.provider", request.Provider);
        activity?.SetTag("payment.amount", request.Amount);
        activity?.SetTag("payment.currency", request.Currency);
        activity?.SetTag("payment.method", request.PaymentMethod);
        activity?.SetTag("payment.merchant.id", request.MerchantId);
        activity?.SetTag("payment.project.code", request.ProjectCode);
        activity?.SetTag("payment.idempotency.key", request.IdempotencyKey);

        try
        {
            // Check if fraud detection feature is enabled (Feature Flags #17, Fraud Detection #22)
            if (await _featureManager.IsEnabledAsync("FraudDetection") && _fraudDetectionService != null)
            {
                activity?.SetTag("fraud.detection.enabled", true);
                _logger.LogInformation("Fraud detection is enabled for payment creation");

                // Extract IP address from HTTP context
                var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString()
                    ?? _httpContextAccessor.HttpContext?.Request?.Headers["X-Forwarded-For"].FirstOrDefault()
                    ?? _httpContextAccessor.HttpContext?.Request?.Headers["X-Real-IP"].FirstOrDefault();

                // Create fraud check request
                var fraudRequest = FraudCheckRequest.Create(
                    amount: request.Amount,
                    currency: request.Currency,
                    paymentMethod: request.PaymentMethod,
                    merchantId: request.MerchantId,
                    orderId: request.OrderId,
                    customerEmail: request.CustomerEmail,
                    customerPhone: request.CustomerPhone,
                    customerId: request.CustomerId,
                    deviceId: request.DeviceId,
                    ipAddress: ipAddress,
                    projectCode: request.ProjectCode,
                    metadata: request.Metadata);

                // Perform fraud check
                var fraudResult = await _fraudDetectionService.CheckAsync(fraudRequest, cancellationToken);

                // Add fraud detection tags to activity
                activity?.SetTag("fraud.risk.level", fraudResult.RiskLevel.ToString());
                activity?.SetTag("fraud.risk.score", fraudResult.RiskScore);
                activity?.SetTag("fraud.recommendation", fraudResult.Recommendation);
                activity?.SetTag("fraud.transaction.id", fraudResult.TransactionId);

                // Handle fraud detection results
                if (fraudResult.ShouldBlock)
                {
                    _logger.LogWarning(
                        "Payment blocked by fraud detection for order {OrderId}. Risk: {RiskLevel}, Score: {RiskScore}, Reasons: {Reasons}",
                        request.OrderId,
                        fraudResult.RiskLevel,
                        fraudResult.RiskScore,
                        string.Join(", ", fraudResult.Reasons));

                    // Record fraud detection metrics
                    _metricsRecorder?.RecordFraudDetection("detected", "blocked");
                    _metricsRecorder?.RecordSuspiciousActivity("fraud", "high");

                    throw new FraudDetectionException(
                        $"Payment blocked due to high fraud risk. Risk score: {fraudResult.RiskScore:F2}. Reasons: {string.Join(", ", fraudResult.Reasons)}",
                        fraudResult);
                }
                else if (fraudResult.ShouldReview)
                {
                    _logger.LogWarning(
                        "Payment flagged for review by fraud detection for order {OrderId}. Risk: {RiskLevel}, Score: {RiskScore}, Reasons: {Reasons}",
                        request.OrderId,
                        fraudResult.RiskLevel,
                        fraudResult.RiskScore,
                        string.Join(", ", fraudResult.Reasons));

                    // Record fraud detection metrics
                    _metricsRecorder?.RecordFraudDetection("detected", "review");
                    _metricsRecorder?.RecordSuspiciousActivity("fraud", "medium");

                    // Log for review but allow payment to proceed
                    // In production, you might want to queue this for manual review
                    activity?.SetTag("fraud.requires.review", true);
                }
                else
                {
                    _logger.LogDebug(
                        "Fraud check passed for order {OrderId}. Risk: {RiskLevel}, Score: {RiskScore}",
                        request.OrderId,
                        fraudResult.RiskLevel,
                        fraudResult.RiskScore);

                    // Record fraud detection metrics
                    _metricsRecorder?.RecordFraudDetection("not_detected", "allowed");
                }
            }
            else
            {
                activity?.SetTag("fraud.detection.enabled", false);
            }

            var createPaymentDto = new CreatePaymentDto(
                request.RequestId,
                request.Amount,
                request.Currency,
                request.PaymentMethod,
                request.Provider,
                request.MerchantId,
                request.OrderId,
                request.ProjectCode,
                request.IdempotencyKey,
                request.SystemFeePercent,
                request.SplitRule,
                request.Metadata,
                request.CallbackUrl,
                request.CustomerEmail,
                request.CustomerPhone,
                request.NfcToken,
                request.DeviceId,
                request.CustomerId);

            var result = await _orchestrator.ProcessPaymentAsync(createPaymentDto, cancellationToken);
            
            activity?.SetTag("payment.id", result.Id.ToString());
            activity?.SetTag("payment.status", result.Status);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment for order {OrderId}", request.OrderId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }
}

