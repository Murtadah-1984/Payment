using Payment.Domain.Common;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Repository interface for optimized reporting queries.
/// Separates read models (OLAP) from write models (OLTP) for performance.
/// </summary>
public interface IPaymentReportRepository
{
    // Monthly aggregations
    Task<decimal> GetTotalProcessedAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalRefundedAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalSystemFeesAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalMerchantPayoutsAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, decimal>> GetTotalByProjectAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<Dictionary<string, decimal>> GetTotalByProviderAsync(int year, int month, CancellationToken cancellationToken = default);

    // Merchant reports
    Task<decimal> GetMerchantTotalProcessedAsync(string merchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<decimal> GetMerchantTotalRefundedAsync(string merchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<int> GetMerchantTransactionCountAsync(string merchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    // Provider reports
    Task<decimal> GetProviderTotalProcessedAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<decimal> GetProviderTotalRefundedAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<int> GetProviderTransactionCountAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<decimal> GetProviderSuccessRateAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    // Refund reports
    Task<IReadOnlyList<Refund>> GetRefundsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<decimal> GetRefundTotalAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetRefundsByReasonAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    // Dispute trace
    Task<PaymentEntity?> GetPaymentWithSplitsAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentSplit>> GetPaymentSplitsAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Refund>> GetPaymentRefundsAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefundAuditLog>> GetRefundAuditLogsAsync(Guid paymentId, CancellationToken cancellationToken = default);

    // Real-time stats
    Task<decimal> GetRealTimeTotalProcessedAsync(DateTime from, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<int> GetRealTimeTransactionCountAsync(DateTime from, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<Dictionary<PaymentStatus, int>> GetRealTimeStatusCountsAsync(DateTime from, string? projectCode = null, CancellationToken cancellationToken = default);

    // Historical data for forecasting
    Task<IReadOnlyList<(int Year, int Month, decimal Total)>> GetHistoricalMonthlyTotalsAsync(
        string? projectCode = null,
        int monthsBack = 12,
        CancellationToken cancellationToken = default);

    // Monthly report aggregation
    /// <summary>
    /// Aggregates all payment data for a specific month.
    /// Returns comprehensive report data including payments, refunds, fees, and splits.
    /// </summary>
    Task<MonthlyReportAggregateData> AggregateMonthlyAsync(DateTime reportMonth, string? projectCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves report metadata to track generated reports and prevent duplicates.
    /// </summary>
    Task SaveReportMetadataAsync(ReportMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a report already exists for the given month and project.
    /// </summary>
    Task<bool> ReportExistsAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregated data for monthly reports.
/// </summary>
public sealed record MonthlyReportAggregateData(
    int Year,
    int Month,
    string? ProjectCode,
    decimal TotalProcessed,
    decimal TotalRefunded,
    decimal TotalSystemFees,
    decimal TotalMerchantPayouts,
    decimal TotalPartnerPayouts,
    Dictionary<string, decimal> TotalByProject,
    Dictionary<string, decimal> TotalByProvider,
    Dictionary<string, decimal> TotalByCurrency,
    int TransactionCount,
    int RefundCount,
    int SuccessfulTransactionCount,
    int FailedTransactionCount);

/// <summary>
/// Metadata for generated reports.
/// </summary>
public sealed record ReportMetadata(
    Guid ReportId,
    int Year,
    int Month,
    string? ProjectCode,
    string ReportUrl,
    string? PdfUrl,
    string? CsvUrl,
    DateTime GeneratedAtUtc,
    string GeneratedBy);

