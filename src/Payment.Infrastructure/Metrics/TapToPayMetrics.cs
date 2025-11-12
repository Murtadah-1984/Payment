using Prometheus;

namespace Payment.Infrastructure.Metrics;

/// <summary>
/// Prometheus metrics for Tap-to-Pay transactions.
/// Exposes metrics for Grafana dashboards and alerting.
/// Follows the same pattern as ReportMetrics for consistency.
/// </summary>
public static class TapToPayMetrics
{
    private static readonly Counter TapToPayTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_tap_to_pay_total",
            "Total number of Tap-to-Pay transactions",
            new CounterConfiguration
            {
                LabelNames = new[] { "status" }
            });

    private static readonly Counter TapToPayFailuresTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_tap_to_pay_failures_total",
            "Total number of Tap-to-Pay transaction failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "error_type" }
            });

    private static readonly Histogram TapToPayDuration = Prometheus.Metrics
        .CreateHistogram(
            "payment_tap_to_pay_duration_seconds",
            "Duration of Tap-to-Pay transaction processing in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "status" },
                Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0 } // 100ms to 30s
            });

    private static readonly Counter TapToPayReplayAttempts = Prometheus.Metrics
        .CreateCounter(
            "payment_tap_to_pay_replay_attempts_total",
            "Total number of detected replay attempts for Tap-to-Pay transactions",
            new CounterConfiguration());

    private static readonly Gauge TapToPayLastTransactionTime = Prometheus.Metrics
        .CreateGauge(
            "payment_tap_to_pay_last_transaction_timestamp",
            "Unix timestamp of last Tap-to-Pay transaction",
            new GaugeConfiguration
            {
                LabelNames = new[] { "status" }
            });

    /// <summary>
    /// Records a successful Tap-to-Pay transaction.
    /// </summary>
    public static void RecordSuccess(double durationSeconds)
    {
        TapToPayTotal.WithLabels("succeeded").Inc();
        TapToPayDuration.WithLabels("succeeded").Observe(durationSeconds);
        TapToPayLastTransactionTime.WithLabels("succeeded").SetToCurrentTimeUtc();
    }

    /// <summary>
    /// Records a failed Tap-to-Pay transaction.
    /// </summary>
    public static void RecordFailure(string errorType, double durationSeconds)
    {
        TapToPayTotal.WithLabels("failed").Inc();
        TapToPayFailuresTotal.WithLabels(errorType).Inc();
        TapToPayDuration.WithLabels("failed").Observe(durationSeconds);
        TapToPayLastTransactionTime.WithLabels("failed").SetToCurrentTimeUtc();
    }

    /// <summary>
    /// Records a detected replay attempt.
    /// </summary>
    public static void RecordReplayAttempt()
    {
        TapToPayReplayAttempts.Inc();
    }
}

