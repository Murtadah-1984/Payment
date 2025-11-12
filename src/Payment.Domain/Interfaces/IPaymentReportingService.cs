namespace Payment.Domain.Interfaces;

/// <summary>
/// High-level reporting service interface (Application layer contract).
/// Orchestrates reporting operations with currency conversion and ML services.
/// </summary>
public interface IPaymentReportingService
{
    Task<object> GetMonthlyReportAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<object> GetMerchantReportAsync(string merchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<object> GetProviderReportAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<object> GetRefundReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<object> GetDisputeTraceAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<object> GetRealTimeStatsAsync(string? projectCode = null, CancellationToken cancellationToken = default);
    Task<object> GetRevenueForecastAsync(string? projectCode, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<object>> GetAnomaliesAsync(string? projectCode, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

