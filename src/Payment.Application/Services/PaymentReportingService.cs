using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Domain.Interfaces;

namespace Payment.Application.Services;

/// <summary>
/// Application service for payment reporting.
/// Orchestrates reporting operations with currency conversion and ML services.
/// Follows SOLID principles - single responsibility for reporting orchestration.
/// </summary>
public class PaymentReportingService : IPaymentReportingService
{
    private readonly IPaymentReportRepository _reportRepository;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IForecastingService _forecastingService;
    private readonly IAnomalyDetectionService _anomalyDetectionService;
    private readonly ILogger<PaymentReportingService> _logger;

    public PaymentReportingService(
        IPaymentReportRepository reportRepository,
        IExchangeRateService exchangeRateService,
        IForecastingService forecastingService,
        IAnomalyDetectionService anomalyDetectionService,
        ILogger<PaymentReportingService> logger)
    {
        _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
        _exchangeRateService = exchangeRateService ?? throw new ArgumentNullException(nameof(exchangeRateService));
        _forecastingService = forecastingService ?? throw new ArgumentNullException(nameof(forecastingService));
        _anomalyDetectionService = anomalyDetectionService ?? throw new ArgumentNullException(nameof(anomalyDetectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<object> GetMonthlyReportAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating monthly report for {Year}-{Month}, ProjectCode: {ProjectCode}", year, month, projectCode);

        var baseCurrency = _exchangeRateService.GetBaseCurrency();

        var totalProcessed = await _reportRepository.GetTotalProcessedAsync(year, month, projectCode, cancellationToken);
        var totalRefunded = await _reportRepository.GetTotalRefundedAsync(year, month, projectCode, cancellationToken);
        var totalSystemFees = await _reportRepository.GetTotalSystemFeesAsync(year, month, projectCode, cancellationToken);
        var totalMerchantPayouts = await _reportRepository.GetTotalMerchantPayoutsAsync(year, month, projectCode, cancellationToken);
        var totalByProject = await _reportRepository.GetTotalByProjectAsync(year, month, cancellationToken);
        var totalByProvider = await _reportRepository.GetTotalByProviderAsync(year, month, cancellationToken);

        // Convert to base currency if needed (simplified - assumes all amounts are in same currency for now)
        // In production, you'd convert each amount based on its original currency

        return new MonthlyReportDto(
            Year: year,
            Month: month,
            TotalProcessed: totalProcessed,
            TotalRefunded: totalRefunded,
            TotalSystemFees: totalSystemFees,
            TotalMerchantPayouts: totalMerchantPayouts,
            TotalByProject: totalByProject,
            TotalByProvider: totalByProvider,
            TransactionCount: 0, // Would need additional query
            RefundCount: 0, // Would need additional query
            BaseCurrency: baseCurrency);
    }

    public async Task<object> GetMerchantReportAsync(string merchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating merchant report for {MerchantId} from {From} to {To}", merchantId, from, to);

        var baseCurrency = _exchangeRateService.GetBaseCurrency();

        var totalProcessed = await _reportRepository.GetMerchantTotalProcessedAsync(merchantId, from, to, cancellationToken);
        var totalRefunded = await _reportRepository.GetMerchantTotalRefundedAsync(merchantId, from, to, cancellationToken);
        var transactionCount = await _reportRepository.GetMerchantTransactionCountAsync(merchantId, from, to, cancellationToken);

        var netRevenue = totalProcessed - totalRefunded;
        var averageTransactionAmount = transactionCount > 0 ? totalProcessed / transactionCount : 0m;
        var refundRate = totalProcessed > 0 ? (totalRefunded / totalProcessed) * 100m : 0m;

        return new MerchantReportDto(
            MerchantId: merchantId,
            From: from,
            To: to,
            TotalProcessed: totalProcessed,
            TotalRefunded: totalRefunded,
            NetRevenue: netRevenue,
            TransactionCount: transactionCount,
            RefundCount: 0, // Would need additional query
            AverageTransactionAmount: averageTransactionAmount,
            RefundRate: refundRate,
            TotalByProvider: new Dictionary<string, decimal>(), // Would need additional query
            BaseCurrency: baseCurrency);
    }

    public async Task<object> GetProviderReportAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating provider report for {Provider} from {From} to {To}", provider, from, to);

        var baseCurrency = _exchangeRateService.GetBaseCurrency();

        var totalProcessed = await _reportRepository.GetProviderTotalProcessedAsync(provider, from, to, cancellationToken);
        var totalRefunded = await _reportRepository.GetProviderTotalRefundedAsync(provider, from, to, cancellationToken);
        var transactionCount = await _reportRepository.GetProviderTransactionCountAsync(provider, from, to, cancellationToken);
        var successRate = await _reportRepository.GetProviderSuccessRateAsync(provider, from, to, cancellationToken);

        var averageTransactionAmount = transactionCount > 0 ? totalProcessed / transactionCount : 0m;
        var refundRate = totalProcessed > 0 ? (totalRefunded / totalProcessed) * 100m : 0m;

        return new ProviderReportDto(
            Provider: provider,
            From: from,
            To: to,
            TotalProcessed: totalProcessed,
            TotalRefunded: totalRefunded,
            TransactionCount: transactionCount,
            SuccessfulTransactionCount: (int)(transactionCount * successRate / 100),
            FailedTransactionCount: transactionCount - (int)(transactionCount * successRate / 100),
            SuccessRate: successRate,
            AverageTransactionAmount: averageTransactionAmount,
            RefundRate: refundRate,
            TotalByProject: new Dictionary<string, decimal>(), // Would need additional query
            BaseCurrency: baseCurrency);
    }

    public async Task<object> GetRefundReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating refund report from {From} to {To}", from, to);

        var baseCurrency = _exchangeRateService.GetBaseCurrency();

        var refunds = await _reportRepository.GetRefundsAsync(from, to, cancellationToken);
        var totalRefunded = await _reportRepository.GetRefundTotalAsync(from, to, cancellationToken);
        var refundsByReason = await _reportRepository.GetRefundsByReasonAsync(from, to, cancellationToken);

        return new RefundReportDto(
            From: from,
            To: to,
            TotalRefunded: totalRefunded,
            RefundCount: refunds.Count,
            RefundsByReason: refundsByReason,
            RefundsByProject: new Dictionary<string, decimal>(), // Would need additional query
            RefundsByProvider: new Dictionary<string, decimal>(), // Would need additional query
            BaseCurrency: baseCurrency);
    }

    public async Task<object> GetDisputeTraceAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating dispute trace for PaymentId: {PaymentId}", paymentId);

        var payment = await _reportRepository.GetPaymentWithSplitsAsync(paymentId, cancellationToken);
        if (payment == null)
        {
            throw new KeyNotFoundException($"Payment with ID {paymentId} not found");
        }

        var splits = await _reportRepository.GetPaymentSplitsAsync(paymentId, cancellationToken);
        var refunds = await _reportRepository.GetPaymentRefundsAsync(paymentId, cancellationToken);
        var auditLogs = await _reportRepository.GetRefundAuditLogsAsync(paymentId, cancellationToken);

        var splitDtos = splits.Select(s => new PaymentSplitDto(
            s.Id,
            s.AccountType,
            s.AccountIdentifier,
            s.Percentage,
            s.Amount)).ToList();

        var refundDtos = refunds.Select(r => new RefundDto(
            r.Id,
            r.Amount.Value,
            r.Currency.Code,
            r.Reason,
            r.Status,
            r.CreatedAt,
            r.ProcessedAt)).ToList();

        var auditLogDtos = auditLogs.Select(a => new RefundAuditLogDto(
            a.Id,
            a.Action,
            a.PerformedBy,
            a.Reason,
            a.Timestamp)).ToList();

        return new DisputeTraceDto(
            PaymentId: payment.Id.Value,
            OrderId: payment.OrderId,
            MerchantId: payment.MerchantId,
            ProjectCode: payment.ProjectCode,
            Amount: payment.Amount.Value,
            Currency: payment.Currency.Code,
            Status: payment.Status,
            CreatedAt: payment.CreatedAt,
            RefundedAt: payment.RefundedAt,
            Splits: splitDtos,
            Refunds: refundDtos,
            AuditLogs: auditLogDtos);
    }

    public async Task<object> GetRealTimeStatsAsync(string? projectCode = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating real-time stats, ProjectCode: {ProjectCode}", projectCode);

        var from = DateTime.UtcNow.AddHours(-24);
        var to = DateTime.UtcNow;

        var baseCurrency = _exchangeRateService.GetBaseCurrency();

        var totalProcessed = await _reportRepository.GetRealTimeTotalProcessedAsync(from, projectCode, cancellationToken);
        var transactionCount = await _reportRepository.GetRealTimeTransactionCountAsync(from, projectCode, cancellationToken);
        var statusCounts = await _reportRepository.GetRealTimeStatusCountsAsync(from, projectCode, cancellationToken);

        return new RealTimeStatsDto(
            From: from,
            To: to,
            TotalProcessed: totalProcessed,
            TransactionCount: transactionCount,
            StatusCounts: statusCounts,
            TotalByProject: new Dictionary<string, decimal>(), // Would need additional query
            TotalByProvider: new Dictionary<string, decimal>(), // Would need additional query
            BaseCurrency: baseCurrency);
    }

    public async Task<object> GetRevenueForecastAsync(string? projectCode, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating revenue forecast for ProjectCode: {ProjectCode}, from {From} to {To}", projectCode, from, to);

        var historicalData = await _reportRepository.GetHistoricalMonthlyTotalsAsync(projectCode, monthsBack: 12, cancellationToken);

        var forecast = await _forecastingService.ForecastRevenueAsync(historicalData, from, to, cancellationToken);

        return forecast;
    }

    public async Task<IReadOnlyList<object>> GetAnomaliesAsync(string? projectCode, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Detecting anomalies for ProjectCode: {ProjectCode}, from {From} to {To}", projectCode, from, to);

        // Get historical data for anomaly detection
        var historicalData = await _reportRepository.GetHistoricalMonthlyTotalsAsync(projectCode, monthsBack: 12, cancellationToken);

        // Convert to format expected by anomaly detection service
        var data = historicalData.Select(h => (
            Date: new DateTime(h.Year, h.Month, 1),
            TotalProcessed: h.Total,
            TotalRefunded: 0m, // Would need additional query
            SystemFees: 0m // Would need additional query
        )).ToList();

        var anomalies = await _anomalyDetectionService.DetectAnomaliesAsync(data, projectCode, cancellationToken);

        return anomalies;
    }
}

