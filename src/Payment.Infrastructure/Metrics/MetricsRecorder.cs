using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Metrics;

/// <summary>
/// Implementation of IMetricsRecorder that records Prometheus metrics.
/// Follows Dependency Inversion Principle - implements Domain interface.
/// Provides comprehensive metrics tracking for business, technical, and security metrics.
/// </summary>
public class MetricsRecorder : IMetricsRecorder
{
    // Report metrics
    public void RecordReportGenerated(string? projectCode, double durationSeconds)
    {
        ReportMetrics.RecordReportGenerated(projectCode, durationSeconds);
    }

    public void RecordReportFailure(string? projectCode, string errorType, double durationSeconds)
    {
        ReportMetrics.RecordReportFailure(projectCode, errorType, durationSeconds);
    }

    // Business metrics
    public void RecordPaymentAttempt(string provider, string status, double durationSeconds)
    {
        PaymentMetrics.RecordPaymentAttempt(provider, status, durationSeconds);
    }

    public void UpdateProviderAvailability(string provider, bool isAvailable)
    {
        PaymentMetrics.UpdateProviderAvailability(provider, isAvailable);
    }

    public void RecordRefund(string provider, string status, double durationSeconds)
    {
        PaymentMetrics.RecordRefund(provider, status, durationSeconds);
    }

    public void RecordFraudDetection(string result, string action)
    {
        PaymentMetrics.RecordFraudDetection(result, action);
    }

    // Technical metrics
    public void RecordApiResponseTime(string endpoint, string method, int statusCode, double durationSeconds)
    {
        PaymentMetrics.RecordApiResponseTime(endpoint, method, statusCode, durationSeconds);
    }

    public void RecordDatabaseQueryTime(string operation, string table, double durationSeconds)
    {
        PaymentMetrics.RecordDatabaseQueryTime(operation, table, durationSeconds);
    }

    public void RecordCacheOperation(string operation, string result)
    {
        PaymentMetrics.RecordCacheOperation(operation, result);
    }

    public void RecordError(string type, string severity, string component)
    {
        PaymentMetrics.RecordError(type, severity, component);
    }

    public void UpdateCircuitBreakerState(string provider, string state)
    {
        var circuitBreakerState = state.ToLower() switch
        {
            "closed" => CircuitBreakerState.Closed,
            "open" => CircuitBreakerState.Open,
            "halfopen" or "half-open" => CircuitBreakerState.HalfOpen,
            _ => CircuitBreakerState.Closed
        };
        PaymentMetrics.UpdateCircuitBreakerState(provider, circuitBreakerState);
    }

    public void UpdateQueueDepth(string queueName, int depth)
    {
        PaymentMetrics.UpdateQueueDepth(queueName, depth);
    }

    // Security metrics
    public void RecordAuthenticationFailure(string reason)
    {
        PaymentMetrics.RecordAuthenticationFailure(reason);
    }

    public void RecordRateLimitHit(string endpoint, string? ipAddress = null)
    {
        PaymentMetrics.RecordRateLimitHit(endpoint, ipAddress);
    }

    public void RecordWebhookSignatureFailure(string provider, string reason)
    {
        PaymentMetrics.RecordWebhookSignatureFailure(provider, reason);
    }

    public void RecordSuspiciousActivity(string type, string severity)
    {
        PaymentMetrics.RecordSuspiciousActivity(type, severity);
    }

    // Alerting metrics
    public void RecordAlertSent(string severity, string channel, string type)
    {
        AlertMetrics.RecordAlertSent(severity, channel, type);
    }

    public void RecordAlertDeduplicated(string severity, string type)
    {
        AlertMetrics.RecordAlertDeduplicated(severity, type);
    }

    public void RecordAlertChannelFailure(string channel, string severity)
    {
        AlertMetrics.RecordAlertChannelFailure(channel, severity);
    }

    public void RecordAlertSendingDuration(string channel, string severity, double durationSeconds)
    {
        AlertMetrics.RecordAlertSendingDuration(channel, severity, durationSeconds);
    }
}

