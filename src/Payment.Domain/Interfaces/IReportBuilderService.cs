namespace Payment.Domain.Interfaces;

/// <summary>
/// Interface for building report files (PDF, CSV).
/// Follows Interface Segregation Principle - focused on report generation only.
/// </summary>
public interface IReportBuilderService
{
    /// <summary>
    /// Generates a PDF report from report data.
    /// </summary>
    /// <param name="reportData">Aggregated report data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF file as byte array</returns>
    Task<byte[]> GeneratePdfAsync(object reportData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a CSV report from report data.
    /// </summary>
    /// <param name="reportData">Aggregated report data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CSV file as byte array</returns>
    Task<byte[]> GenerateCsvAsync(object reportData, CancellationToken cancellationToken = default);
}

