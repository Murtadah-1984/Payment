namespace Payment.Domain.Interfaces;

/// <summary>
/// Service for detecting anomalies in payment patterns.
/// Pluggable implementation - can be replaced with ML models.
/// </summary>
public interface IAnomalyDetectionService
{
    /// <summary>
    /// Detects anomalies in payment data for the specified period.
    /// </summary>
    Task<IReadOnlyList<object>> DetectAnomaliesAsync(
        IEnumerable<(DateTime Date, decimal TotalProcessed, decimal TotalRefunded, decimal SystemFees)> data,
        string? projectCode = null,
        CancellationToken cancellationToken = default);
}

