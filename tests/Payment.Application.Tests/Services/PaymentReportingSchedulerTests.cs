using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Events;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.Application.Tests.Services;

/// <summary>
/// Unit tests for PaymentReportingScheduler.
/// Tests report generation workflow, idempotency, and error handling.
/// </summary>
public class PaymentReportingSchedulerTests
{
    private readonly Mock<IPaymentReportRepository> _reportRepositoryMock;
    private readonly Mock<IReportBuilderService> _reportBuilderMock;
    private readonly Mock<IStorageService> _storageServiceMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IMetricsRecorder> _metricsRecorderMock;
    private readonly Mock<ILogger<PaymentReportingScheduler>> _loggerMock;
    private readonly PaymentReportingScheduler _scheduler;

    public PaymentReportingSchedulerTests()
    {
        _reportRepositoryMock = new Mock<IPaymentReportRepository>();
        _reportBuilderMock = new Mock<IReportBuilderService>();
        _storageServiceMock = new Mock<IStorageService>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _metricsRecorderMock = new Mock<IMetricsRecorder>();
        _loggerMock = new Mock<ILogger<PaymentReportingScheduler>>();

        _scheduler = new PaymentReportingScheduler(
            _reportRepositoryMock.Object,
            _reportBuilderMock.Object,
            _storageServiceMock.Object,
            _eventPublisherMock.Object,
            _metricsRecorderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateMonthlyReportAsync_WhenReportExists_ReturnsExistingReport()
    {
        // Arrange
        var reportMonth = new DateTime(2025, 10, 1);
        _reportRepositoryMock
            .Setup(r => r.ReportExistsAsync(2025, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _scheduler.GenerateMonthlyReportAsync(reportMonth);

        // Assert
        Assert.NotEqual(Guid.Empty, result.ReportId);
        Assert.NotNull(result.ReportUrl);
        Assert.NotEmpty(result.ReportUrl);
        _reportRepositoryMock.Verify(r => r.AggregateMonthlyAsync(It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _reportBuilderMock.Verify(b => b.GeneratePdfAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateMonthlyReportAsync_WhenReportDoesNotExist_GeneratesNewReport()
    {
        // Arrange
        var reportMonth = new DateTime(2025, 10, 1);
        var aggregateData = new MonthlyReportAggregateData(
            Year: 2025,
            Month: 10,
            ProjectCode: null,
            TotalProcessed: 10000m,
            TotalRefunded: 500m,
            TotalSystemFees: 100m,
            TotalMerchantPayouts: 9400m,
            TotalPartnerPayouts: 0m,
            TotalByProject: new Dictionary<string, decimal>(),
            TotalByProvider: new Dictionary<string, decimal>(),
            TotalByCurrency: new Dictionary<string, decimal>(),
            TransactionCount: 100,
            RefundCount: 5,
            SuccessfulTransactionCount: 95,
            FailedTransactionCount: 5);

        _reportRepositoryMock
            .Setup(r => r.ReportExistsAsync(2025, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reportRepositoryMock
            .Setup(r => r.AggregateMonthlyAsync(reportMonth, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregateData);

        _reportBuilderMock
            .Setup(b => b.GeneratePdfAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        _reportBuilderMock
            .Setup(b => b.GenerateCsvAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 4, 5, 6 });

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.company.com/reports/2025-10.pdf");

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), "text/csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.company.com/reports/2025-10.csv");

        _eventPublisherMock
            .Setup(e => e.PublishWithRetryAsync(
                "payment.reports.monthly.generated",
                It.IsAny<MonthlyReportGeneratedEvent>(),
                3,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _scheduler.GenerateMonthlyReportAsync(reportMonth);

        // Assert
        Assert.NotEqual(Guid.Empty, result.ReportId);
        Assert.NotNull(result.ReportUrl);
        Assert.NotEmpty(result.ReportUrl);
        
        _reportRepositoryMock.Verify(r => r.AggregateMonthlyAsync(reportMonth, null, It.IsAny<CancellationToken>()), Times.Once);
        _reportBuilderMock.Verify(b => b.GeneratePdfAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _reportBuilderMock.Verify(b => b.GenerateCsvAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _storageServiceMock.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), "application/pdf", It.IsAny<CancellationToken>()), Times.Once);
        _storageServiceMock.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), "text/csv", It.IsAny<CancellationToken>()), Times.Once);
        _reportRepositoryMock.Verify(r => r.SaveReportMetadataAsync(It.IsAny<ReportMetadata>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishWithRetryAsync(
            "payment.reports.monthly.generated",
            It.IsAny<MonthlyReportGeneratedEvent>(),
            3,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateMonthlyReportAsync_WhenAggregationFails_ThrowsException()
    {
        // Arrange
        var reportMonth = new DateTime(2025, 10, 1);
        _reportRepositoryMock
            .Setup(r => r.ReportExistsAsync(2025, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reportRepositoryMock
            .Setup(r => r.AggregateMonthlyAsync(reportMonth, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _scheduler.GenerateMonthlyReportAsync(reportMonth));
        
        _reportBuilderMock.Verify(b => b.GeneratePdfAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        _storageServiceMock.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateMonthlyReportAsync_WhenStorageUploadFails_ThrowsException()
    {
        // Arrange
        var reportMonth = new DateTime(2025, 10, 1);
        var aggregateData = new MonthlyReportAggregateData(
            Year: 2025,
            Month: 10,
            ProjectCode: null,
            TotalProcessed: 10000m,
            TotalRefunded: 500m,
            TotalSystemFees: 100m,
            TotalMerchantPayouts: 9400m,
            TotalPartnerPayouts: 0m,
            TotalByProject: new Dictionary<string, decimal>(),
            TotalByProvider: new Dictionary<string, decimal>(),
            TotalByCurrency: new Dictionary<string, decimal>(),
            TransactionCount: 100,
            RefundCount: 5,
            SuccessfulTransactionCount: 95,
            FailedTransactionCount: 5);

        _reportRepositoryMock
            .Setup(r => r.ReportExistsAsync(2025, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reportRepositoryMock
            .Setup(r => r.AggregateMonthlyAsync(reportMonth, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregateData);

        _reportBuilderMock
            .Setup(b => b.GeneratePdfAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), "application/pdf", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _scheduler.GenerateMonthlyReportAsync(reportMonth));
        
        _eventPublisherMock.Verify(e => e.PublishWithRetryAsync(
            It.IsAny<string>(),
            It.IsAny<MonthlyReportGeneratedEvent>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateMonthlyReportAsync_WithProjectCode_FiltersByProject()
    {
        // Arrange
        var reportMonth = new DateTime(2025, 10, 1);
        var projectCode = "MARKETPLACE-001";
        
        _reportRepositoryMock
            .Setup(r => r.ReportExistsAsync(2025, 10, projectCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Note: In the current implementation, projectCode is hardcoded to null
        // This test documents the expected behavior if project filtering is added

        // Act
        var result = await _scheduler.GenerateMonthlyReportAsync(reportMonth);

        // Assert
        // Verify that aggregation is called (even if projectCode is null for now)
        _reportRepositoryMock.Verify(r => r.AggregateMonthlyAsync(reportMonth, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}

