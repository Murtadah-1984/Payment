using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Domain.Events;
using Payment.Domain.Interfaces;
using System.Diagnostics;

namespace Payment.Application.Services;

/// <summary>
/// Service for scheduling and orchestrating monthly report generation.
/// Follows Single Responsibility Principle - orchestrates report generation workflow.
/// Implements idempotency to prevent duplicate report creation.
/// </summary>
public class PaymentReportingScheduler : IPaymentReportingScheduler
{
    private readonly IPaymentReportRepository _reportRepository;
    private readonly IReportBuilderService _reportBuilder;
    private readonly IStorageService _storageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IMetricsRecorder _metricsRecorder;
    private readonly ILogger<PaymentReportingScheduler> _logger;

    public PaymentReportingScheduler(
        IPaymentReportRepository reportRepository,
        IReportBuilderService reportBuilder,
        IStorageService storageService,
        IEventPublisher eventPublisher,
        IMetricsRecorder metricsRecorder,
        ILogger<PaymentReportingScheduler> logger)
    {
        _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
        _reportBuilder = reportBuilder ?? throw new ArgumentNullException(nameof(reportBuilder));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _metricsRecorder = metricsRecorder ?? throw new ArgumentNullException(nameof(metricsRecorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(Guid ReportId, string ReportUrl, string? PdfUrl, string? CsvUrl)> GenerateMonthlyReportAsync(
        DateTime reportMonth,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var year = reportMonth.Year;
        var month = reportMonth.Month;
        var projectCode = (string?)null; // Generate report for all projects by default

        _logger.LogInformation(
            "Starting monthly report generation for {Year}-{Month:D2}, ProjectCode: {ProjectCode}",
            year, month, projectCode ?? "ALL");

        try
        {
            // Check idempotency - prevent duplicate report generation
            var reportExists = await _reportRepository.ReportExistsAsync(year, month, projectCode, cancellationToken);
            if (reportExists)
            {
                _logger.LogWarning(
                    "Report already exists for {Year}-{Month:D2}, ProjectCode: {ProjectCode}. Skipping generation.",
                    year, month, projectCode ?? "ALL");
                
                // Return existing report metadata (would need to fetch from DB in production)
                var existingReportId = Guid.NewGuid(); // In production, fetch from existing report
                var existingReportUrl = $"reports/{year}-{month:D2}.pdf";
                return (existingReportId, existingReportUrl, existingReportUrl, null);
            }

            // Step 1: Aggregate monthly data
            _logger.LogInformation("Aggregating monthly data for {Year}-{Month:D2}", year, month);
            var aggregateData = await _reportRepository.AggregateMonthlyAsync(reportMonth, projectCode, cancellationToken);

            // Step 2: Create report data DTO
            var reportData = new MonthlyReportDataDto(
                Year: aggregateData.Year,
                Month: aggregateData.Month,
                ProjectCode: aggregateData.ProjectCode,
                TotalProcessed: aggregateData.TotalProcessed,
                TotalRefunded: aggregateData.TotalRefunded,
                TotalSystemFees: aggregateData.TotalSystemFees,
                TotalMerchantPayouts: aggregateData.TotalMerchantPayouts,
                TotalPartnerPayouts: aggregateData.TotalPartnerPayouts,
                TotalByProject: aggregateData.TotalByProject,
                TotalByProvider: aggregateData.TotalByProvider,
                TotalByCurrency: aggregateData.TotalByCurrency,
                TransactionCount: aggregateData.TransactionCount,
                RefundCount: aggregateData.RefundCount,
                SuccessfulTransactionCount: aggregateData.SuccessfulTransactionCount,
                FailedTransactionCount: aggregateData.FailedTransactionCount,
                GeneratedAtUtc: DateTime.UtcNow);

            // Step 3: Generate report files (PDF and CSV)
            _logger.LogInformation("Generating PDF and CSV reports");
            var pdfBytes = await _reportBuilder.GeneratePdfAsync(reportData, cancellationToken);
            var csvBytes = await _reportBuilder.GenerateCsvAsync(reportData, cancellationToken);

            // Step 4: Upload files to storage
            _logger.LogInformation("Uploading reports to storage");
            var reportId = Guid.NewGuid();
            var pdfFileName = $"reports/{year}-{month:D2}{(projectCode != null ? $"-{projectCode}" : "")}.pdf";
            var csvFileName = $"reports/{year}-{month:D2}{(projectCode != null ? $"-{projectCode}" : "")}.csv";

            var pdfUrl = await _storageService.UploadAsync(pdfFileName, pdfBytes, "application/pdf", cancellationToken);
            var csvUrl = await _storageService.UploadAsync(csvFileName, csvBytes, "text/csv", cancellationToken);

            // Use PDF URL as primary report URL
            var reportUrl = pdfUrl;

            // Step 5: Save report metadata
            _logger.LogInformation("Saving report metadata");
            var metadata = new ReportMetadata(
                ReportId: reportId,
                Year: year,
                Month: month,
                ProjectCode: projectCode,
                ReportUrl: reportUrl,
                PdfUrl: pdfUrl,
                CsvUrl: csvUrl,
                GeneratedAtUtc: DateTime.UtcNow,
                GeneratedBy: "System");

            await _reportRepository.SaveReportMetadataAsync(metadata, cancellationToken);

            // Step 6: Publish domain event to notification microservice
            _logger.LogInformation("Publishing MonthlyReportGeneratedEvent");
            var domainEvent = new MonthlyReportGeneratedEvent(
                ReportId: reportId,
                Year: year,
                Month: month,
                ProjectCode: projectCode ?? "ALL",
                ReportUrl: reportUrl,
                GeneratedAtUtc: DateTime.UtcNow);

            // Publish with retry policy (exponential backoff)
            await _eventPublisher.PublishWithRetryAsync(
                "payment.reports.monthly.generated",
                domainEvent,
                maxRetries: 3,
                cancellationToken);

            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;
            
            // Record Prometheus metrics
            _metricsRecorder.RecordReportGenerated(projectCode, durationSeconds);
            
            _logger.LogInformation(
                "Monthly report generated successfully in {Duration}ms. ReportId: {ReportId}, ReportUrl: {ReportUrl}",
                stopwatch.ElapsedMilliseconds, reportId, reportUrl);

            return (reportId, reportUrl, pdfUrl, csvUrl);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;
            var errorType = ex.GetType().Name;
            
            // Record Prometheus metrics for failure
            _metricsRecorder.RecordReportFailure(projectCode, errorType, durationSeconds);
            
            _logger.LogError(ex,
                "Failed to generate monthly report for {Year}-{Month:D2} after {Duration}ms",
                year, month, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

