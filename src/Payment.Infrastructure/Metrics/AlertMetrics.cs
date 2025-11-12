using Prometheus;

namespace Payment.Infrastructure.Metrics;

/// <summary>
/// Prometheus metrics for alerting system.
/// Exposes metrics for Grafana dashboards and alerting.
/// Follows the same pattern as PaymentMetrics and ReportMetrics for consistency.
/// </summary>
public static class AlertMetrics
{
    /// <summary>
    /// Total number of alerts sent.
    /// </summary>
    private static readonly Counter AlertsSentTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_alerts_sent_total",
            "Total number of alerts sent",
            new CounterConfiguration
            {
                LabelNames = new[] { "severity", "channel", "type" }
            });

    /// <summary>
    /// Total number of alerts that were deduplicated.
    /// </summary>
    private static readonly Counter AlertsDeduplicatedTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_alerts_deduplicated_total",
            "Total number of alerts that were deduplicated",
            new CounterConfiguration
            {
                LabelNames = new[] { "severity", "type" }
            });

    /// <summary>
    /// Total number of alert channel failures.
    /// </summary>
    private static readonly Counter AlertChannelFailuresTotal = Prometheus.Metrics
        .CreateCounter(
            "payment_alert_channel_failures_total",
            "Total number of alert channel failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "channel", "severity" }
            });

    /// <summary>
    /// Duration of alert sending in seconds.
    /// </summary>
    private static readonly Histogram AlertSendingDuration = Prometheus.Metrics
        .CreateHistogram(
            "payment_alert_sending_duration_seconds",
            "Duration of alert sending in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "channel", "severity" },
                Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0 } // 100ms to 30s
            });

    /// <summary>
    /// Records a sent alert.
    /// </summary>
    public static void RecordAlertSent(string severity, string channel, string type)
    {
        AlertsSentTotal.WithLabels(severity, channel, type).Inc();
    }

    /// <summary>
    /// Records a deduplicated alert.
    /// </summary>
    public static void RecordAlertDeduplicated(string severity, string type)
    {
        AlertsDeduplicatedTotal.WithLabels(severity, type).Inc();
    }

    /// <summary>
    /// Records an alert channel failure.
    /// </summary>
    public static void RecordAlertChannelFailure(string channel, string severity)
    {
        AlertChannelFailuresTotal.WithLabels(channel, severity).Inc();
    }

    /// <summary>
    /// Records the duration of alert sending.
    /// </summary>
    public static void RecordAlertSendingDuration(string channel, string severity, double durationSeconds)
    {
        AlertSendingDuration.WithLabels(channel, severity).Observe(durationSeconds);
    }
}

