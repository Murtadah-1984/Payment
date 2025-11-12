using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Simple anomaly detection service using statistical methods.
/// Can be replaced with ML models for more sophisticated detection.
/// </summary>
public class AnomalyDetectionService : IAnomalyDetectionService
{
    private readonly ILogger<AnomalyDetectionService> _logger;

    public AnomalyDetectionService(ILogger<AnomalyDetectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<object>> DetectAnomaliesAsync(
        IEnumerable<(DateTime Date, decimal TotalProcessed, decimal TotalRefunded, decimal SystemFees)> data,
        string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        var dataList = data.ToList();
        if (dataList.Count < 3)
        {
            _logger.LogWarning("Insufficient data for anomaly detection (need at least 3 data points)");
            return Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
        }

        var anomalies = new List<object>();

        // Calculate statistics for total processed
        var processedValues = dataList.Select(d => (double)d.TotalProcessed).ToList();
        var mean = processedValues.Average();
        var stdDev = Math.Sqrt(processedValues.Select(v => Math.Pow(v - mean, 2)).Average());

        // Detect anomalies using Z-score (values > 2 standard deviations)
        var threshold = mean + (2 * stdDev);
        var lowerThreshold = mean - (2 * stdDev);

        foreach (var point in dataList)
        {
            var zScore = stdDev > 0 ? Math.Abs((double)point.TotalProcessed - mean) / stdDev : 0;

            if (zScore > 2.0)
            {
                var severity = zScore > 3.0 ? "High" : "Medium";
                anomalies.Add(new
                {
                    DetectedAt = point.Date,
                    Type = point.TotalProcessed > (decimal)threshold ? "Volume Spike" : "Volume Drop",
                    Severity = severity,
                    Description = $"Unusual transaction volume detected: {point.TotalProcessed:F2} (expected range: {lowerThreshold:F2} - {threshold:F2})",
                    ExpectedValue = (decimal)mean,
                    ActualValue = point.TotalProcessed,
                    ProjectCode = projectCode,
                    Metadata = new Dictionary<string, object>
                    {
                        { "ZScore", zScore },
                        { "StandardDeviation", (decimal)stdDev }
                    }
                });
            }

            // Check refund rate anomalies
            if (point.TotalProcessed > 0)
            {
                var refundRate = (point.TotalRefunded / point.TotalProcessed) * 100m;
                if (refundRate > 10m) // Alert if refund rate > 10%
                {
                    anomalies.Add(new
                    {
                        DetectedAt = point.Date,
                        Type = "High Refund Rate",
                        Severity = refundRate > 20m ? "High" : "Medium",
                        Description = $"Unusually high refund rate detected: {refundRate:F2}%",
                        ExpectedValue = 5m, // Expected refund rate
                        ActualValue = refundRate,
                        ProjectCode = projectCode,
                        Metadata = new Dictionary<string, object>
                        {
                            { "TotalProcessed", point.TotalProcessed },
                            { "TotalRefunded", point.TotalRefunded }
                        }
                    });
                }
            }
        }

        _logger.LogInformation("Detected {Count} anomalies", anomalies.Count);

        return Task.FromResult<IReadOnlyList<object>>(anomalies);
    }
}

