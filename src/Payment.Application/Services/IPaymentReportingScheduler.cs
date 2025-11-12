namespace Payment.Application.Services;

/// <summary>
/// Interface for scheduling and orchestrating monthly report generation.
/// Follows Single Responsibility Principle - focused on report scheduling only.
/// </summary>
public interface IPaymentReportingScheduler
{
    /// <summary>
    /// Generates a monthly financial report for the specified month.
    /// Aggregates all payments, refunds, fees, and splits.
    /// Generates PDF and CSV files, uploads to storage, and publishes event.
    /// </summary>
    /// <param name="reportMonth">The month to generate the report for (day is ignored, only year/month matter)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report ID and URLs</returns>
    Task<(Guid ReportId, string ReportUrl, string? PdfUrl, string? CsvUrl)> GenerateMonthlyReportAsync(
        DateTime reportMonth,
        CancellationToken cancellationToken = default);
}

