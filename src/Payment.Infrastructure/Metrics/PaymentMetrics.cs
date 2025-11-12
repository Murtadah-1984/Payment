using Prometheus;
using Metrics = Prometheus.Metrics;

namespace Payment.Infrastructure.Metrics;

/// <summary>
/// Comprehensive Prometheus metrics for payment operations.
/// Tracks business, technical, and security metrics as specified in remediation instructions.
/// Follows the same pattern as TapToPayMetrics and ReportMetrics for consistency.
/// </summary>
public static class PaymentMetrics
{
    #region Business Metrics

    /// <summary>
    /// Total number of payment attempts (successful and failed).
    /// Used to calculate payment success rate.
    /// </summary>
    private static readonly Counter PaymentTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_total",
            "Total number of payment attempts",
            new CounterConfiguration
            {
                LabelNames = new[] { "provider", "status" }
            });

    /// <summary>
    /// Duration of payment processing in seconds.
    /// Used to calculate average payment processing time (target: <2s).
    /// </summary>
    private static readonly Histogram PaymentProcessingDuration = Prometheus.Metrics
        .CreateHistogram(
            "payment_processing_duration_seconds",
            "Duration of payment processing in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "provider", "status" },
                Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0 } // 100ms to 30s
            });

    /// <summary>
    /// Provider availability status (1 = available, 0 = unavailable).
    /// Used to track provider availability (target: >99.9%).
    /// </summary>
    private static readonly Gauge ProviderAvailability = Prometheus.Metrics
        .CreateGauge(
            "payment_provider_availability",
            "Provider availability status (1 = available, 0 = unavailable)",
            new GaugeConfiguration
            {
                LabelNames = new[] { "provider" }
            });

    /// <summary>
    /// Total number of refunds processed.
    /// Used to calculate refund rate.
    /// </summary>
    private static readonly Counter RefundTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_refund_total",
            "Total number of refunds processed",
            new CounterConfiguration
            {
                LabelNames = new[] { "provider", "status" }
            });

    /// <summary>
    /// Duration of refund processing in seconds.
    /// </summary>
    private static readonly Histogram RefundProcessingDuration = Prometheus.Metrics
        .CreateHistogram(
            "payment_refund_processing_duration_seconds",
            "Duration of refund processing in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "provider", "status" },
                Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0 }
            });

    /// <summary>
    /// Fraud detection accuracy metrics.
    /// </summary>
    private static readonly Counter FraudDetectionTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_fraud_detection_total",
            "Total number of fraud detection checks",
            new CounterConfiguration
            {
                LabelNames = new[] { "result", "action" } // result: detected, not_detected | action: blocked, allowed
            });

    #endregion

    #region Technical Metrics

    /// <summary>
    /// API response time in seconds (p50, p95, p99).
    /// Tracked by OpenTelemetry, but we add custom tracking for specific endpoints.
    /// </summary>
    private static readonly Histogram ApiResponseTime = Prometheus.Metrics
        .CreateHistogram(
            "payment_api_response_time_seconds",
            "API response time in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "endpoint", "method", "status_code" },
                Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0 } // 10ms to 10s
            });

    /// <summary>
    /// Database query time in seconds.
    /// Tracked by OpenTelemetry EF Core instrumentation, but we add custom tracking for critical queries.
    /// </summary>
    private static readonly Histogram DatabaseQueryTime = Prometheus.Metrics
        .CreateHistogram(
            "payment_database_query_time_seconds",
            "Database query execution time in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation", "table" },
                Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 } // 1ms to 1s
            });

    /// <summary>
    /// Cache hit/miss counters.
    /// Used to calculate cache hit rate (target: >80%).
    /// </summary>
    private static readonly Counter CacheOperations = Prometheus.Metrics
        .CreateCounter(
            "payment_cache_operations_total",
            "Total number of cache operations",
            new CounterConfiguration
            {
                LabelNames = new[] { "operation", "result" } // operation: get, set, remove | result: hit, miss
            });

    /// <summary>
    /// Total number of errors encountered.
    /// Used to calculate error rate (target: <0.1%).
    /// </summary>
    private static readonly Counter ErrorTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_errors_total",
            "Total number of errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "type", "severity", "component" } // severity: critical, error, warning
            });

    /// <summary>
    /// Circuit breaker state (0 = closed, 1 = open, 2 = half-open).
    /// </summary>
    private static readonly Gauge CircuitBreakerStateGauge = Prometheus.Metrics
        .CreateGauge(
            "payment_circuit_breaker_state",
            "Circuit breaker state (0 = closed, 1 = open, 2 = half-open)",
            new GaugeConfiguration
            {
                LabelNames = new[] { "provider" }
            });

    /// <summary>
    /// Queue depth for payment processing queues.
    /// </summary>
    private static readonly Gauge QueueDepth = Prometheus.Metrics
        .CreateGauge(
            "payment_queue_depth",
            "Current depth of payment processing queues",
            new GaugeConfiguration
            {
                LabelNames = new[] { "queue_name" }
            });

    #endregion

    #region Security Metrics

    /// <summary>
    /// Total number of failed authentication attempts.
    /// </summary>
    private static readonly Counter AuthenticationFailures = Prometheus.Metrics
        .CreateCounter(
            "payment_authentication_failures_total",
            "Total number of failed authentication attempts",
            new CounterConfiguration
            {
                LabelNames = new[] { "reason" } // reason: invalid_token, expired_token, missing_token, etc.
            });

    /// <summary>
    /// Total number of rate limit hits.
    /// </summary>
    private static readonly Counter RateLimitHits = Prometheus.Metrics
        .CreateCounter(
            "payment_rate_limit_hits_total",
            "Total number of rate limit hits",
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "ip_address" }
            });

    /// <summary>
    /// Total number of webhook signature validation failures.
    /// </summary>
    private static readonly Counter WebhookSignatureFailures = Prometheus.Metrics
        .CreateCounter(
            "payment_webhook_signature_failures_total",
            "Total number of webhook signature validation failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "provider", "reason" } // reason: invalid_signature, missing_signature, expired_timestamp
            });

    /// <summary>
    /// Total number of suspicious activity alerts.
    /// </summary>
    private static readonly Counter SuspiciousActivityAlerts = Prometheus.Metrics
        .CreateCounter(
            "payment_suspicious_activity_alerts_total",
            "Total number of suspicious activity alerts",
            new CounterConfiguration
            {
                LabelNames = new[] { "type", "severity" } // type: fraud, replay_attack, unusual_pattern | severity: high, medium, low
            });

    #endregion

    #region Business Metrics Methods

    /// <summary>
    /// Records a payment attempt (successful or failed).
    /// </summary>
    public static void RecordPaymentAttempt(string provider, string status, double durationSeconds)
    {
        PaymentTotal.WithLabels(provider, status).Inc();
        PaymentProcessingDuration.WithLabels(provider, status).Observe(durationSeconds);
    }

    /// <summary>
    /// Updates provider availability status.
    /// </summary>
    public static void UpdateProviderAvailability(string provider, bool isAvailable)
    {
        ProviderAvailability.WithLabels(provider).Set(isAvailable ? 1.0 : 0.0);
    }

    /// <summary>
    /// Records a refund attempt.
    /// </summary>
    public static void RecordRefund(string provider, string status, double durationSeconds)
    {
        RefundTotal.WithLabels(provider, status).Inc();
        RefundProcessingDuration.WithLabels(provider, status).Observe(durationSeconds);
    }

    /// <summary>
    /// Records a fraud detection check.
    /// </summary>
    public static void RecordFraudDetection(string result, string action)
    {
        FraudDetectionTotal.WithLabels(result, action).Inc();
    }

    #endregion

    #region Technical Metrics Methods

    /// <summary>
    /// Records API response time.
    /// </summary>
    public static void RecordApiResponseTime(string endpoint, string method, int statusCode, double durationSeconds)
    {
        ApiResponseTime.WithLabels(endpoint, method, statusCode.ToString()).Observe(durationSeconds);
    }

    /// <summary>
    /// Records database query execution time.
    /// </summary>
    public static void RecordDatabaseQueryTime(string operation, string table, double durationSeconds)
    {
        DatabaseQueryTime.WithLabels(operation, table).Observe(durationSeconds);
    }

    /// <summary>
    /// Records a cache operation (hit or miss).
    /// </summary>
    public static void RecordCacheOperation(string operation, string result)
    {
        CacheOperations.WithLabels(operation, result).Inc();
    }

    /// <summary>
    /// Records an error.
    /// </summary>
    public static void RecordError(string type, string severity, string component)
    {
        ErrorTotal.WithLabels(type, severity, component).Inc();
    }

    /// <summary>
    /// Updates circuit breaker state.
    /// </summary>
    public static void UpdateCircuitBreakerState(string provider, CircuitBreakerState state)
    {
        var stateValue = state switch
        {
            CircuitBreakerState.Closed => 0.0,
            CircuitBreakerState.Open => 1.0,
            CircuitBreakerState.HalfOpen => 2.0,
            _ => 0.0
        };
        CircuitBreakerStateGauge.WithLabels(provider).Set(stateValue);
    }

    /// <summary>
    /// Updates queue depth.
    /// </summary>
    public static void UpdateQueueDepth(string queueName, int depth)
    {
        QueueDepth.WithLabels(queueName).Set(depth);
    }

    #endregion

    #region Security Metrics Methods

    /// <summary>
    /// Records a failed authentication attempt.
    /// </summary>
    public static void RecordAuthenticationFailure(string reason)
    {
        AuthenticationFailures.WithLabels(reason).Inc();
    }

    /// <summary>
    /// Records a rate limit hit.
    /// </summary>
    public static void RecordRateLimitHit(string endpoint, string? ipAddress = null)
    {
        RateLimitHits.WithLabels(endpoint, ipAddress ?? "unknown").Inc();
    }

    /// <summary>
    /// Records a webhook signature validation failure.
    /// </summary>
    public static void RecordWebhookSignatureFailure(string provider, string reason)
    {
        WebhookSignatureFailures.WithLabels(provider, reason).Inc();
    }

    /// <summary>
    /// Records a suspicious activity alert.
    /// </summary>
    public static void RecordSuspiciousActivity(string type, string severity)
    {
        SuspiciousActivityAlerts.WithLabels(type, severity).Inc();
    }

    #endregion
}

/// <summary>
/// Circuit breaker state enumeration for metrics.
/// </summary>
public enum CircuitBreakerState
{
    Closed = 0,
    Open = 1,
    HalfOpen = 2
}

