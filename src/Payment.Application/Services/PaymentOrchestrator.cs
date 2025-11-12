using System.Text.Json;
using Payment.Application.DTOs;
using Payment.Application.Mappings;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Domain.Exceptions;
using Payment.Domain.Entities;
using Payment.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Orchestrator that coordinates payment processing workflow.
/// Follows Single Responsibility Principle - only coordinates the workflow.
/// Delegates specific responsibilities to specialized services.
/// </summary>
public class PaymentOrchestrator : IPaymentOrchestrator
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentProviderFactory _providerFactory;
    private readonly ISplitPaymentService _splitPaymentService;
    private readonly IMetadataEnrichmentService _metadataEnrichmentService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IRequestHashService _requestHashService;
    private readonly IPaymentFactory _paymentFactory;
    private readonly IPaymentProcessingService _paymentProcessingService;
    private readonly IPaymentStatusUpdater _paymentStatusUpdater;
    private readonly IPaymentStateService _stateService;
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<PaymentOrchestrator> _logger;
    private readonly IMetricsRecorder _metricsRecorder;

    public PaymentOrchestrator(
        IUnitOfWork unitOfWork,
        IPaymentProviderFactory providerFactory,
        ISplitPaymentService splitPaymentService,
        IMetadataEnrichmentService metadataEnrichmentService,
        IIdempotencyService idempotencyService,
        IRequestHashService requestHashService,
        IPaymentFactory paymentFactory,
        IPaymentProcessingService paymentProcessingService,
        IPaymentStatusUpdater paymentStatusUpdater,
        IPaymentStateService stateService,
        IFeatureManager featureManager,
        ILogger<PaymentOrchestrator> logger,
        IMetricsRecorder metricsRecorder)
    {
        _unitOfWork = unitOfWork;
        _providerFactory = providerFactory;
        _splitPaymentService = splitPaymentService;
        _metadataEnrichmentService = metadataEnrichmentService;
        _idempotencyService = idempotencyService;
        _requestHashService = requestHashService;
        _paymentFactory = paymentFactory;
        _paymentProcessingService = paymentProcessingService;
        _paymentStatusUpdater = paymentStatusUpdater;
        _stateService = stateService;
        _featureManager = featureManager;
        _logger = logger;
        _metricsRecorder = metricsRecorder;
    }

    public async Task<PaymentDto> ProcessPaymentAsync(CreatePaymentDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing payment for order {OrderId} with provider {Provider}, requestId={RequestId}, projectCode={ProjectCode}, idempotencyKey={IdempotencyKey}", 
            request.OrderId, request.Provider, request.RequestId, request.ProjectCode, request.IdempotencyKey);

        // Step 1: Check idempotency using idempotency key
        var existingRequest = await _unitOfWork.IdempotentRequests
            .GetByKeyAsync(request.IdempotencyKey, cancellationToken);

        if (existingRequest != null)
        {
            // Verify request hash matches (prevent key reuse with different data)
            var currentHash = _requestHashService.ComputeRequestHash(request);
            if (existingRequest.RequestHash != currentHash)
            {
                _logger.LogWarning(
                    "Idempotency key {IdempotencyKey} reused with different request data. Existing hash: {ExistingHash}, Current hash: {CurrentHash}",
                    request.IdempotencyKey, existingRequest.RequestHash, currentHash);
                
                throw new IdempotencyKeyMismatchException(
                    $"Idempotency key '{request.IdempotencyKey}' was previously used with different request data. " +
                    "Each idempotency key must be unique to a specific request.");
            }

            // Return existing payment
            var existingPayment = await _unitOfWork.Payments
                .GetByIdAsync(existingRequest.PaymentId, cancellationToken);

            if (existingPayment == null)
            {
                _logger.LogError(
                    "Idempotency record found for key {IdempotencyKey} but payment {PaymentId} not found",
                    request.IdempotencyKey, existingRequest.PaymentId);
                throw new InvalidOperationException(
                    $"Payment {existingRequest.PaymentId} referenced by idempotency key not found");
            }

            _logger.LogInformation(
                "Idempotent request detected for key {IdempotencyKey}, returning existing payment {PaymentId}",
                request.IdempotencyKey, existingPayment.Id.Value);
            
            return existingPayment.ToDto();
        }

        // Step 2: Enrich metadata (delegated to IMetadataEnrichmentService)
        var metadata = _metadataEnrichmentService.EnrichMetadata(request, request.Metadata);
        
        // Add Tap-to-Pay specific fields to metadata if provided
        if (!string.IsNullOrEmpty(request.NfcToken))
        {
            metadata = metadata ?? new Dictionary<string, string>();
            metadata["nfc_token"] = request.NfcToken;
        }
        
        if (!string.IsNullOrEmpty(request.DeviceId))
        {
            metadata = metadata ?? new Dictionary<string, string>();
            metadata["device_id"] = request.DeviceId;
        }
        
        if (!string.IsNullOrEmpty(request.CustomerId))
        {
            metadata = metadata ?? new Dictionary<string, string>();
            metadata["customer_id"] = request.CustomerId;
        }

        // Step 3: Calculate split payment (delegated to ISplitPaymentService) - Feature Flags #17
        SplitPayment? splitPayment = null;
        var splitPaymentsEnabled = await _featureManager.IsEnabledAsync("SplitPayments", cancellationToken);
        
        if (!splitPaymentsEnabled && (request.SplitRule != null || request.SystemFeePercent.HasValue))
        {
            _logger.LogWarning(
                "Split payment requested but SplitPayments feature flag is disabled. OrderId={OrderId}",
                request.OrderId);
            throw new InvalidOperationException(
                "Split payments feature is currently disabled. Please contact support to enable this feature.");
        }
        
        if (splitPaymentsEnabled)
        {
            if (request.SplitRule != null)
            {
                var (split, splitDetails) = _splitPaymentService.CalculateMultiAccountSplit(request.Amount, request.SplitRule);
                splitPayment = split;
                
                // Store detailed split information in metadata
                var splitDetailsJson = JsonSerializer.Serialize(splitDetails);
                metadata["split_details"] = splitDetailsJson;
                
                _logger.LogInformation("Multi-account split payment calculated: System={SystemShare}, Owner={OwnerShare}, Accounts={AccountCount}",
                    splitPayment.SystemShare, splitPayment.OwnerShare, request.SplitRule.Accounts.Count);
            }
            else if (request.SystemFeePercent.HasValue && request.SystemFeePercent.Value > 0)
            {
                splitPayment = _splitPaymentService.CalculateSplit(request.Amount, request.SystemFeePercent.Value);
                _logger.LogInformation("Simple split payment calculated: System={SystemShare}, Owner={OwnerShare}, Fee={FeePercent}%",
                    splitPayment.SystemShare, splitPayment.OwnerShare, splitPayment.SystemFeePercent);
            }
        }

        // Step 4: Create payment entity (delegated to IPaymentFactory)
        var payment = _paymentFactory.CreatePayment(request, splitPayment, metadata);

        // Step 5: Persist payment
        await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Step 6: Get payment provider (delegated to IPaymentProviderFactory) - Feature Flags #17
        var provider = await _providerFactory.CreateAsync(request.Provider, cancellationToken);

        // Step 7: Process payment through provider (delegated to IPaymentProcessingService)
        var processingStartTime = DateTime.UtcNow;
        try
        {
            var result = await _paymentProcessingService.ProcessPaymentAsync(payment, provider, cancellationToken);

            // Step 8: Update payment status (delegated to IPaymentStatusUpdater)
            _paymentStatusUpdater.UpdatePaymentStatus(payment, result);

            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            
            // Step 9: Store idempotency record
            var requestHash = _requestHashService.ComputeRequestHash(request);
            var idempotentRequest = new IdempotentRequest(
                idempotencyKey: request.IdempotencyKey,
                paymentId: payment.Id.Value,
                requestHash: requestHash,
                createdAt: DateTime.UtcNow,
                expiresAt: DateTime.UtcNow.AddHours(24));

            await _unitOfWork.IdempotentRequests.AddAsync(idempotentRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Record metrics for successful payment
            var processingDuration = (DateTime.UtcNow - processingStartTime).TotalSeconds;
            var status = result.Success ? "succeeded" : "failed";
            _metricsRecorder.RecordPaymentAttempt(request.Provider, status, processingDuration);
            _metricsRecorder.UpdateProviderAvailability(request.Provider, result.Success);

            _logger.LogInformation(
                "Payment {PaymentId} created and idempotency record stored for key {IdempotencyKey}",
                payment.Id.Value, request.IdempotencyKey);

            return payment.ToDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment {PaymentId} with provider {Provider}",
                payment.Id.Value, request.Provider);
            
            payment.Fail($"Provider error: {ex.Message}", _stateService);
            await _unitOfWork.Payments.UpdateAsync(payment, cancellationToken);
            
            // Record metrics for failed payment
            var processingDuration = (DateTime.UtcNow - processingStartTime).TotalSeconds;
            _metricsRecorder.RecordPaymentAttempt(request.Provider, "failed", processingDuration);
            _metricsRecorder.UpdateProviderAvailability(request.Provider, false);
            _metricsRecorder.RecordError("payment_processing", "error", "PaymentOrchestrator");
            
            // Store idempotency record even if payment fails (to prevent retries)
            var requestHash = _requestHashService.ComputeRequestHash(request);
            var idempotentRequest = new IdempotentRequest(
                idempotencyKey: request.IdempotencyKey,
                paymentId: payment.Id.Value,
                requestHash: requestHash,
                createdAt: DateTime.UtcNow,
                expiresAt: DateTime.UtcNow.AddHours(24));

            await _unitOfWork.IdempotentRequests.AddAsync(idempotentRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            throw;
        }
    }
}

