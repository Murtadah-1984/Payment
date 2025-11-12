using Payment.Domain.Entities;

namespace Payment.Domain.Entities;

/// <summary>
/// Entity to track generated monthly reports and prevent duplicates.
/// Follows Clean Architecture - domain entity in Domain layer.
/// </summary>
public class ReportMetadata : Entity
{
    public Guid Id { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string? ProjectCode { get; private set; }
    public string ReportUrl { get; private set; } = string.Empty;
    public string? PdfUrl { get; private set; }
    public string? CsvUrl { get; private set; }
    public DateTime GeneratedAtUtc { get; private set; }
    public string GeneratedBy { get; private set; } = string.Empty;

    private ReportMetadata() { } // EF Core

    public ReportMetadata(
        Guid id,
        int year,
        int month,
        string? projectCode,
        string reportUrl,
        string? pdfUrl,
        string? csvUrl,
        DateTime generatedAtUtc,
        string generatedBy)
    {
        Id = id;
        Year = year;
        Month = month;
        ProjectCode = projectCode;
        ReportUrl = reportUrl;
        PdfUrl = pdfUrl;
        CsvUrl = csvUrl;
        GeneratedAtUtc = generatedAtUtc;
        GeneratedBy = generatedBy;
    }
}

