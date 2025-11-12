using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Services;

/// <summary>
/// Service for responding to payment failure incidents.
/// Follows SOLID principles - single responsibility for incident response.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class IncidentResponseService : IIncidentResponseService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICircuitBreakerService _circuitBreakerService;
    private readonly IRefundService _refundService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<IncidentResponseService> _logger;

    public IncidentResponseService(
        IPaymentRepository paymentRepository,
        ICircuitBreakerService circuitBreakerService,
        IRefundService refundService,
        INotificationService notificationService,
        ILogger<IncidentResponseService> logger)
    {
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _circuitBreakerService = circuitBreakerService ?? throw new ArgumentNullException(nameof(circuitBreakerService));
        _refundService = refundService ?? throw new ArgumentNullException(nameof(refundService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IncidentAssessment> AssessPaymentFailureAsync(
        PaymentFailureContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        _logger.LogInformation(
            "Assessing payment failure incident. Provider: {Provider}, FailureType: {FailureType}, AffectedCount: {AffectedCount}",
            context.Provider, context.FailureType, context.AffectedPaymentCount);

        // Determine severity based on affected payment count and failure type
        var severity = DetermineSeverity(context);
        
        // Identify root cause
        var rootCause = DetermineRootCause(context);
        
        // Get affected providers
        var affectedProviders = await GetAffectedProvidersAsync(context, cancellationToken);
        
        // Estimate resolution time
        var estimatedResolutionTime = EstimateResolutionTime(context, severity);
        
        // Generate recommended actions
        var recommendedActions = GenerateRecommendedActions(context, severity, affectedProviders);

        var assessment = IncidentAssessment.Create(
            severity: severity,
            rootCause: rootCause,
            affectedProviders: affectedProviders,
            affectedPaymentCount: context.AffectedPaymentCount,
            estimatedResolutionTime: estimatedResolutionTime,
            recommendedActions: recommendedActions);

        _logger.LogInformation(
            "Incident assessment completed. Severity: {Severity}, RootCause: {RootCause}, RecommendedActions: {ActionCount}",
            severity, rootCause, recommendedActions.Count());

        return assessment;
    }

    public async Task<bool> NotifyStakeholdersAsync(
        IncidentSeverity severity,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        _logger.LogInformation(
            "Notifying stakeholders about incident. Severity: {Severity}, Message: {Message}",
            severity, message);

        try
        {
            var success = await _notificationService.NotifyStakeholdersAsync(severity, message, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Stakeholder notification sent successfully");
            }
            else
            {
                _logger.LogWarning("Failed to send stakeholder notification");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending stakeholder notification");
            return false;
        }
    }

    public async Task<Dictionary<PaymentId, bool>> ProcessAutomaticRefundsAsync(
        IEnumerable<PaymentId> paymentIds,
        CancellationToken cancellationToken = default)
    {
        if (paymentIds == null)
            throw new ArgumentNullException(nameof(paymentIds));

        var paymentIdList = paymentIds.ToList();
        if (paymentIdList.Count == 0)
        {
            _logger.LogWarning("No payment IDs provided for automatic refunds");
            return new Dictionary<PaymentId, bool>();
        }

        _logger.LogInformation(
            "Processing automatic refunds for {Count} payments",
            paymentIdList.Count);

        var reason = "Automatic refund due to payment failure incident";
        var results = await _refundService.ProcessRefundsAsync(paymentIdList, reason, cancellationToken);

        var successCount = results.Values.Count(r => r);
        var failureCount = results.Values.Count(r => !r);

        _logger.LogInformation(
            "Automatic refunds processed. Success: {SuccessCount}, Failed: {FailureCount}",
            successCount, failureCount);

        return results;
    }

    public async Task<IncidentMetricsDto> GetIncidentMetricsAsync(
        TimeRange timeRange,
        CancellationToken cancellationToken = default)
    {
        if (timeRange == null)
            throw new ArgumentNullException(nameof(timeRange));

        _logger.LogInformation(
            "Getting incident metrics for time range: {Start} to {End}",
            timeRange.Start, timeRange.End);

        // Query failed payments in the time range
        var failedPayments = await GetFailedPaymentsInRangeAsync(timeRange, cancellationToken);
        
        // Group by provider and failure type
        var incidentsByProvider = failedPayments
            .GroupBy(p => p.Provider?.ToString() ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var incidentsByFailureType = new Dictionary<PaymentFailureType, int>();
        foreach (var payment in failedPayments)
        {
            var failureType = InferFailureType(payment);
            incidentsByFailureType.TryGetValue(failureType, out var count);
            incidentsByFailureType[failureType] = count + 1;
        }

        // Calculate severity distribution (simplified - would need incident history in production)
        var totalIncidents = failedPayments.Count;
        var criticalIncidents = failedPayments.Count(p => ShouldBeCritical(p));
        var highSeverityIncidents = failedPayments.Count(p => ShouldBeHigh(p)) - criticalIncidents;
        var mediumSeverityIncidents = failedPayments.Count(p => ShouldBeMedium(p));
        var lowSeverityIncidents = totalIncidents - criticalIncidents - highSeverityIncidents - mediumSeverityIncidents;

        // Calculate average resolution time (simplified - would need actual resolution data)
        var averageResolutionTime = TimeSpan.FromMinutes(15); // Placeholder

        // Convert incidentsByFailureType to string dictionary for DTO
        var incidentsByType = incidentsByFailureType.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value);

        var metrics = IncidentMetricsDto.Create(
            totalIncidents: totalIncidents,
            criticalIncidents: criticalIncidents,
            highSeverityIncidents: highSeverityIncidents,
            mediumSeverityIncidents: mediumSeverityIncidents,
            lowSeverityIncidents: lowSeverityIncidents,
            averageResolutionTime: averageResolutionTime,
            incidentsByType: incidentsByType);

        _logger.LogInformation(
            "Incident metrics retrieved. TotalIncidents: {TotalIncidents}, Critical: {Critical}",
            totalIncidents, criticalIncidents);

        return metrics;
    }

    private IncidentSeverity DetermineSeverity(PaymentFailureContext context)
    {
        // Critical: > 100 payments affected or provider unavailable
        if (context.AffectedPaymentCount > 100 || 
            context.FailureType == PaymentFailureType.ProviderUnavailable)
        {
            return IncidentSeverity.Critical;
        }

        // High: 50-100 payments affected or timeout/network errors
        if (context.AffectedPaymentCount >= 50 ||
            context.FailureType == PaymentFailureType.Timeout ||
            context.FailureType == PaymentFailureType.NetworkError)
        {
            return IncidentSeverity.High;
        }

        // Medium: 10-50 payments affected
        if (context.AffectedPaymentCount >= 10)
        {
            return IncidentSeverity.Medium;
        }

        // Low: < 10 payments affected
        return IncidentSeverity.Low;
    }

    private string DetermineRootCause(PaymentFailureContext context)
    {
        return context.FailureType switch
        {
            PaymentFailureType.ProviderUnavailable => "Payment provider is unavailable or circuit breaker is open",
            PaymentFailureType.ProviderError => "Payment provider returned an error response",
            PaymentFailureType.Timeout => "Payment processing timed out",
            PaymentFailureType.Declined => "Payments were declined by the provider",
            PaymentFailureType.NetworkError => "Network connectivity issue with payment provider",
            PaymentFailureType.AuthenticationError => "Authentication or authorization failure with provider",
            PaymentFailureType.ValidationError => "Invalid payment data or configuration",
            _ => "Unknown payment failure cause"
        };
    }

    private async Task<IEnumerable<string>> GetAffectedProvidersAsync(
        PaymentFailureContext context,
        CancellationToken cancellationToken)
    {
        var providers = new HashSet<string>();

        if (!string.IsNullOrWhiteSpace(context.Provider))
        {
            providers.Add(context.Provider);
        }

        // Check for providers with open circuit breakers
        var providersWithOpenCircuitBreakers = await _circuitBreakerService
            .GetProvidersWithOpenCircuitBreakersAsync(cancellationToken);
        
        foreach (var provider in providersWithOpenCircuitBreakers)
        {
            providers.Add(provider);
        }

        return providers;
    }

    private TimeSpan EstimateResolutionTime(
        PaymentFailureContext context,
        IncidentSeverity severity)
    {
        return severity switch
        {
            IncidentSeverity.Critical => TimeSpan.FromMinutes(30),
            IncidentSeverity.High => TimeSpan.FromMinutes(15),
            IncidentSeverity.Medium => TimeSpan.FromMinutes(10),
            IncidentSeverity.Low => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(15)
        };
    }

    private IEnumerable<RecommendedAction> GenerateRecommendedActions(
        PaymentFailureContext context,
        IncidentSeverity severity,
        IEnumerable<string> affectedProviders)
    {
        var actions = new List<RecommendedAction>();

        // If provider is unavailable, recommend switching provider
        if (context.FailureType == PaymentFailureType.ProviderUnavailable)
        {
            var alternativeProviders = GetAlternativeProviders(affectedProviders);
            if (alternativeProviders.Any())
            {
                actions.Add(RecommendedAction.SwitchProvider(alternativeProviders.First()));
            }
        }

        // For critical/high severity, recommend escalation
        if (severity >= IncidentSeverity.High)
        {
            actions.Add(RecommendedAction.EscalateToTeam("Payment Operations"));
        }

        // Recommend processing refunds if applicable
        if (context.AffectedPaymentCount > 0 && 
            (context.FailureType == PaymentFailureType.ProviderError ||
             context.FailureType == PaymentFailureType.Timeout))
        {
            actions.Add(RecommendedAction.ProcessRefunds());
        }

        // Recommend contacting provider for persistent issues
        if (context.FailureType == PaymentFailureType.ProviderError ||
            context.FailureType == PaymentFailureType.AuthenticationError)
        {
            actions.Add(RecommendedAction.ContactProvider());
        }

        // Recommend retrying payments after resolution
        if (context.FailureType == PaymentFailureType.Timeout ||
            context.FailureType == PaymentFailureType.NetworkError)
        {
            actions.Add(RecommendedAction.RetryPayments());
        }

        return actions;
    }

    private IEnumerable<string> GetAlternativeProviders(IEnumerable<string> affectedProviders)
    {
        // In production, this would query available providers from configuration
        var allProviders = new[] { "Stripe", "Checkout", "Helcim", "ZainCash" };
        return allProviders.Except(affectedProviders, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<PaymentEntity>> GetFailedPaymentsInRangeAsync(
        TimeRange timeRange,
        CancellationToken cancellationToken)
    {
        // Get all failed payments
        var failedPayments = await _paymentRepository.GetByStatusAsync(PaymentStatus.Failed, cancellationToken);
        
        // Filter by time range
        return failedPayments
            .Where(p => timeRange.Contains(p.CreatedAt))
            .ToList();
    }

    private PaymentFailureType InferFailureType(PaymentEntity payment)
    {
        // Infer failure type from payment failure reason
        var failureReason = payment.FailureReason?.ToLowerInvariant() ?? string.Empty;

        if (failureReason.Contains("timeout") || failureReason.Contains("timed out"))
        {
            return PaymentFailureType.Timeout;
        }

        if (failureReason.Contains("network") || failureReason.Contains("connection"))
        {
            return PaymentFailureType.NetworkError;
        }

        if (failureReason.Contains("declined") || failureReason.Contains("denied"))
        {
            return PaymentFailureType.Declined;
        }

        if (failureReason.Contains("auth") || failureReason.Contains("unauthorized"))
        {
            return PaymentFailureType.AuthenticationError;
        }

        if (failureReason.Contains("invalid") || failureReason.Contains("validation"))
        {
            return PaymentFailureType.ValidationError;
        }

        return PaymentFailureType.ProviderError;
    }

    private bool ShouldBeCritical(PaymentEntity payment)
    {
        // Critical if amount is high or multiple failures
        return payment.Amount.Value > 10000m;
    }

    private bool ShouldBeHigh(PaymentEntity payment)
    {
        // High if amount is moderate
        return payment.Amount.Value > 1000m;
    }

    private bool ShouldBeMedium(PaymentEntity payment)
    {
        // Medium for other cases
        return true;
    }
}

