namespace Payment.Domain.Interfaces;

/// <summary>
/// Service for revenue forecasting using historical data.
/// Pluggable implementation - can be replaced with ML models.
/// </summary>
public interface IForecastingService
{
    /// <summary>
    /// Generates revenue forecast for the specified period.
    /// </summary>
    Task<object> ForecastRevenueAsync(
        IEnumerable<(int Year, int Month, decimal Total)> historicalData,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}

