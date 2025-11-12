using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Data;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Infrastructure.Repositories;

/// <summary>
/// Optimized repository for reporting queries.
/// Uses materialized views and efficient aggregations for performance.
/// </summary>
public class PaymentReportRepository : IPaymentReportRepository
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<PaymentReportRepository> _logger;

    public PaymentReportRepository(PaymentDbContext context, ILogger<PaymentReportRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal> GetTotalProcessedAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .Where(p => p.CreatedAt.Year == year && p.CreatedAt.Month == month)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        return await query.SumAsync(p => p.Amount.Value, cancellationToken);
    }

    public async Task<decimal> GetTotalRefundedAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .Where(p => p.RefundedAt.HasValue && 
                       p.RefundedAt.Value.Year == year && 
                       p.RefundedAt.Value.Month == month)
            .Where(p => p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartiallyRefunded);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        return await query.SumAsync(p => p.Amount.Value, cancellationToken);
    }

    public async Task<decimal> GetTotalSystemFeesAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .Where(p => p.CreatedAt.Year == year && p.CreatedAt.Month == month)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        return await query.SumAsync(p => p.SystemFeeAmount, cancellationToken);
    }

    public async Task<decimal> GetTotalMerchantPayoutsAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .Where(p => p.CreatedAt.Year == year && p.CreatedAt.Month == month)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        return await query.SumAsync(p => p.Amount.Value - p.SystemFeeAmount, cancellationToken);
    }

    public async Task<Dictionary<string, decimal>> GetTotalByProjectAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var results = await _context.Payments
            .Where(p => p.CreatedAt.Year == year && p.CreatedAt.Month == month)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .Where(p => !string.IsNullOrEmpty(p.ProjectCode))
            .GroupBy(p => p.ProjectCode!)
            .Select(g => new { ProjectCode = g.Key, Total = g.Sum(p => p.Amount.Value) })
            .ToListAsync(cancellationToken);

        return results.ToDictionary(r => r.ProjectCode, r => r.Total);
    }

    public async Task<Dictionary<string, decimal>> GetTotalByProviderAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var results = await _context.Payments
            .Where(p => p.CreatedAt.Year == year && p.CreatedAt.Month == month)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.Provider.Name)
            .Select(g => new { Provider = g.Key, Total = g.Sum(p => p.Amount.Value) })
            .ToListAsync(cancellationToken);

        return results.ToDictionary(r => r.Provider, r => r.Total);
    }

    public async Task<decimal> GetMerchantTotalProcessedAsync(string merchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.MerchantId == merchantId)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount.Value, cancellationToken);
    }

    public async Task<decimal> GetMerchantTotalRefundedAsync(string merchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.MerchantId == merchantId)
            .Where(p => p.RefundedAt.HasValue && p.RefundedAt.Value >= from && p.RefundedAt.Value <= to)
            .Where(p => p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartiallyRefunded)
            .SumAsync(p => p.Amount.Value, cancellationToken);
    }

    public async Task<int> GetMerchantTransactionCountAsync(string merchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.MerchantId == merchantId)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .CountAsync(cancellationToken);
    }

    public async Task<decimal> GetProviderTotalProcessedAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.Provider.Name == provider)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount.Value, cancellationToken);
    }

    public async Task<decimal> GetProviderTotalRefundedAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.Provider.Name == provider)
            .Where(p => p.RefundedAt.HasValue && p.RefundedAt.Value >= from && p.RefundedAt.Value <= to)
            .Where(p => p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartiallyRefunded)
            .SumAsync(p => p.Amount.Value, cancellationToken);
    }

    public async Task<int> GetProviderTransactionCountAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.Provider.Name == provider)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .CountAsync(cancellationToken);
    }

    public async Task<decimal> GetProviderSuccessRateAsync(string provider, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var total = await _context.Payments
            .Where(p => p.Provider.Name == provider)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .CountAsync(cancellationToken);

        if (total == 0) return 0m;

        var successful = await _context.Payments
            .Where(p => p.Provider.Name == provider)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .CountAsync(cancellationToken);

        return (decimal)successful / total * 100m;
    }

    public async Task<IReadOnlyList<Refund>> GetRefundsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetRefundTotalAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .Where(r => r.Status == PaymentStatus.Succeeded || r.Status == PaymentStatus.Completed)
            .SumAsync(r => r.Amount.Value, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetRefundsByReasonAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var results = await _context.Refunds
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .GroupBy(r => r.Reason)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return results.ToDictionary(r => r.Reason, r => r.Count);
    }

    public async Task<PaymentEntity?> GetPaymentWithSplitsAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.Id.Value == paymentId, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentSplit>> GetPaymentSplitsAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentSplits
            .Where(ps => ps.PaymentId.Value == paymentId)
            .OrderBy(ps => ps.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Refund>> GetPaymentRefundsAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds
            .Where(r => r.PaymentId.Value == paymentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefundAuditLog>> GetRefundAuditLogsAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.RefundAuditLogs
            .Where(ral => ral.PaymentId.Value == paymentId)
            .OrderBy(ral => ral.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetRealTimeTotalProcessedAsync(DateTime from, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .Where(p => p.CreatedAt >= from)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        return await query.SumAsync(p => p.Amount.Value, cancellationToken);
    }

    public async Task<int> GetRealTimeTransactionCountAsync(DateTime from, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .Where(p => p.CreatedAt >= from);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<Dictionary<PaymentStatus, int>> GetRealTimeStatusCountsAsync(DateTime from, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Payments
            .Where(p => p.CreatedAt >= from);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        var results = await query
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return results.ToDictionary(r => r.Status, r => r.Count);
    }

    public async Task<IReadOnlyList<(int Year, int Month, decimal Total)>> GetHistoricalMonthlyTotalsAsync(
        string? projectCode = null,
        int monthsBack = 12,
        CancellationToken cancellationToken = default)
    {
        var from = DateTime.UtcNow.AddMonths(-monthsBack);
        var fromDate = new DateTime(from.Year, from.Month, 1);

        var query = _context.Payments
            .Where(p => p.CreatedAt >= fromDate)
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        var results = await query
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Total = g.Sum(p => p.Amount.Value)
            })
            .OrderBy(r => r.Year)
            .ThenBy(r => r.Month)
            .ToListAsync(cancellationToken);

        return results.Select(r => (r.Year, r.Month, r.Total)).ToList();
    }

    public async Task<MonthlyReportAggregateData> AggregateMonthlyAsync(DateTime reportMonth, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var year = reportMonth.Year;
        var month = reportMonth.Month;
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Get all payments for the month
        var paymentsQuery = _context.Payments
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate);

        if (!string.IsNullOrEmpty(projectCode))
        {
            paymentsQuery = paymentsQuery.Where(p => p.ProjectCode == projectCode);
        }

        var payments = await paymentsQuery.ToListAsync(cancellationToken);

        // Get all refunds for the month
        var refundsQuery = _context.Refunds
            .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate);

        if (!string.IsNullOrEmpty(projectCode))
        {
            // Refunds are linked to payments, so we need to join
            refundsQuery = refundsQuery
                .Where(r => _context.Payments.Any(p => p.Id.Value == r.PaymentId.Value && p.ProjectCode == projectCode));
        }

        var refunds = await refundsQuery.ToListAsync(cancellationToken);

        // Calculate totals
        var totalProcessed = payments
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount.Value);

        var totalRefunded = refunds
            .Where(r => r.Status == PaymentStatus.Succeeded || r.Status == PaymentStatus.Completed)
            .Sum(r => r.Amount.Value);

        var totalSystemFees = payments
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .Sum(p => p.SystemFeeAmount);

        var totalMerchantPayouts = payments
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount.Value - p.SystemFeeAmount);

        // Calculate partner payouts from payment splits
        var totalPartnerPayouts = await _context.PaymentSplits
            .Where(ps => payments.Any(p => p.Id.Value == ps.PaymentId.Value))
            .Where(ps => ps.AccountType == "Partner")
            .SumAsync(ps => ps.Amount, cancellationToken);

        // Group by project
        var totalByProject = payments
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .Where(p => !string.IsNullOrEmpty(p.ProjectCode))
            .GroupBy(p => p.ProjectCode!)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount.Value));

        // Group by provider
        var totalByProvider = payments
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.Provider.Name)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount.Value));

        // Group by currency
        var totalByCurrency = payments
            .Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.Currency.Code)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount.Value));

        var transactionCount = payments.Count;
        var refundCount = refunds.Count;
        var successfulTransactionCount = payments.Count(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Completed);
        var failedTransactionCount = payments.Count(p => p.Status == PaymentStatus.Failed);

        return new MonthlyReportAggregateData(
            Year: year,
            Month: month,
            ProjectCode: projectCode,
            TotalProcessed: totalProcessed,
            TotalRefunded: totalRefunded,
            TotalSystemFees: totalSystemFees,
            TotalMerchantPayouts: totalMerchantPayouts,
            TotalPartnerPayouts: totalPartnerPayouts,
            TotalByProject: totalByProject,
            TotalByProvider: totalByProvider,
            TotalByCurrency: totalByCurrency,
            TransactionCount: transactionCount,
            RefundCount: refundCount,
            SuccessfulTransactionCount: successfulTransactionCount,
            FailedTransactionCount: failedTransactionCount);
    }

    public async Task SaveReportMetadataAsync(Domain.Interfaces.ReportMetadata metadata, CancellationToken cancellationToken = default)
    {
        var entity = new Payment.Domain.Entities.ReportMetadata(
            metadata.ReportId,
            metadata.Year,
            metadata.Month,
            metadata.ProjectCode,
            metadata.ReportUrl,
            metadata.PdfUrl,
            metadata.CsvUrl,
            metadata.GeneratedAtUtc,
            metadata.GeneratedBy);

        _context.ReportMetadata.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ReportExistsAsync(int year, int month, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ReportMetadata
            .Where(r => r.Year == year && r.Month == month);

        if (!string.IsNullOrEmpty(projectCode))
        {
            query = query.Where(r => r.ProjectCode == projectCode);
        }
        else
        {
            query = query.Where(r => r.ProjectCode == null);
        }

        return await query.AnyAsync(cancellationToken);
    }
}

