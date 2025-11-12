namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for recording metrics.
/// Follows Dependency Inversion Principle - Application layer depends on abstraction.
/// Covers all business, technical, and security metrics as specified in remediation instructions.
/// </summary>
public interface IMetricsRecorder
{
    // Report metrics
    void RecordReportGenerated(string? projectCode, double durationSeconds);
    void RecordReportFailure(string? projectCode, string errorType, double durationSeconds);

    // Business metrics
    void RecordPaymentAttempt(string provider, string status, double durationSeconds);
    void UpdateProviderAvailability(string provider, bool isAvailable);
    void RecordRefund(string provider, string status, double durationSeconds);
    void RecordFraudDetection(string result, string action);

    // Technical metrics
    void RecordApiResponseTime(string endpoint, string method, int statusCode, double durationSeconds);
    void RecordDatabaseQueryTime(string operation, string table, double durationSeconds);
    void RecordCacheOperation(string operation, string result);
    void RecordError(string type, string severity, string component);
    void UpdateCircuitBreakerState(string provider, string state);
    void UpdateQueueDepth(string queueName, int depth);

    // Security metrics
    void RecordAuthenticationFailure(string reason);
    void RecordRateLimitHit(string endpoint, string? ipAddress = null);
    void RecordWebhookSignatureFailure(string provider, string reason);
    void RecordSuspiciousActivity(string type, string severity);

    // Alerting metrics
    void RecordAlertSent(string severity, string channel, string type);
    void RecordAlertDeduplicated(string severity, string type);
    void RecordAlertChannelFailure(string channel, string severity);
    void RecordAlertSendingDuration(string channel, string severity, double durationSeconds);
}

