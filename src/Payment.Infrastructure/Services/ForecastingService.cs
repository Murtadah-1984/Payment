using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Simple forecasting service using moving average.
/// Can be replaced with ML models for more sophisticated forecasting.
/// </summary>
public class ForecastingService : IForecastingService
{
    private readonly ILogger<ForecastingService> _logger;

    public ForecastingService(ILogger<ForecastingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<object> ForecastRevenueAsync(
        IEnumerable<(int Year, int Month, decimal Total)> historicalData,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var data = historicalData.ToList();
        if (data.Count == 0)
        {
            _logger.LogWarning("No historical data available for forecasting");
            return Task.FromResult<object>(new
            {
                ForecastedRevenue = 0m,
                ConfidenceLower = 0m,
                ConfidenceUpper = 0m,
                Periods = new List<object>()
            });
        }

        // Simple moving average forecast
        var average = data.Average(d => d.Total);
        var variance = data.Count > 1 
            ? data.Select(d => Math.Pow((double)(d.Total - average), 2)).Average()
            : 0.0;
        var stdDev = Math.Sqrt(variance);

        // Calculate number of periods to forecast
        var monthsToForecast = ((to.Year - from.Year) * 12) + (to.Month - from.Month) + 1;
        var periods = new List<object>();

        var currentDate = from;
        var forecastedTotal = 0m;

        for (int i = 0; i < monthsToForecast; i++)
        {
            var forecastedAmount = (decimal)average;
            var confidenceLower = (decimal)(average - (1.96 * stdDev)); // 95% confidence interval
            var confidenceUpper = (decimal)(average + (1.96 * stdDev));

            periods.Add(new
            {
                Period = currentDate,
                ForecastedAmount = forecastedAmount,
                ConfidenceLower = Math.Max(0, confidenceLower),
                ConfidenceUpper = confidenceUpper
            });

            forecastedTotal += forecastedAmount;
            currentDate = currentDate.AddMonths(1);
        }

        var result = new
        {
            ForecastedRevenue = forecastedTotal,
            ConfidenceLower = Math.Max(0, forecastedTotal - (decimal)(1.96 * stdDev * monthsToForecast)),
            ConfidenceUpper = forecastedTotal + (decimal)(1.96 * stdDev * monthsToForecast),
            Periods = periods
        };

        _logger.LogInformation("Generated forecast: {ForecastedRevenue} with {Periods} periods", result.ForecastedRevenue, periods.Count);

        return Task.FromResult<object>(result);
    }
}

