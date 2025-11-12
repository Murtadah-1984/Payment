using Prometheus;

namespace Payment.Infrastructure.Metrics;

/// <summary>
/// Prometheus metrics for report generation.
/// Exposes metrics for Grafana dashboards and alerting.
/// </summary>
public static class ReportMetrics
{
    private static readonly Counter ReportsGeneratedTotal = Metrics
        .CreateCounter(
            "payment_reports_generated_total",
            "Total number of monthly reports generated",
            new CounterConfiguration
            {
                LabelNames = new[] { "project", "status" }
            });

    private static readonly Counter ReportsFailuresTotal = Metrics
        .CreateCounter(
            "payment_reports_failures_total",
            "Total number of report generation failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "project", "error_type" }
            });

    private static readonly Histogram ReportGenerationDuration = Metrics
        .CreateHistogram(
            "payment_reports_last_duration_seconds",
            "Duration of report generation in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "project" },
                Buckets = new[] { 1.0, 5.0, 10.0, 30.0, 60.0, 120.0, 300.0 } // 1s to 5min
            });

    private static readonly Gauge LastReportGenerationTime = Metrics
        .CreateGauge(
            "payment_reports_last_generation_timestamp",
            "Unix timestamp of last successful report generation",
            new GaugeConfiguration
            {
                LabelNames = new[] { "project" }
            });

    /// <summary>
    /// Records a successful report generation.
    /// </summary>
    public static void RecordReportGenerated(string? projectCode, double durationSeconds)
    {
        var project = projectCode ?? "ALL";
        ReportsGeneratedTotal.WithLabels(project, "success").Inc();
        ReportGenerationDuration.WithLabels(project).Observe(durationSeconds);
        LastReportGenerationTime.WithLabels(project).SetToCurrentTimeUtc();
    }

    /// <summary>
    /// Records a failed report generation.
    /// </summary>
    public static void RecordReportFailure(string? projectCode, string errorType, double durationSeconds)
    {
        var project = projectCode ?? "ALL";
        ReportsGeneratedTotal.WithLabels(project, "failure").Inc();
        ReportsFailuresTotal.WithLabels(project, errorType).Inc();
        ReportGenerationDuration.WithLabels(project).Observe(durationSeconds);
    }
}

