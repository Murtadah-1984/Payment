using Payment.Domain.Enums;

namespace Payment.Application.DTOs;

/// <summary>
/// Monthly report DTO with comprehensive financial aggregations.
/// </summary>
public sealed record MonthlyReportDto(
    int Year,
    int Month,
    decimal TotalProcessed,
    decimal TotalRefunded,
    decimal TotalSystemFees,
    decimal TotalMerchantPayouts,
    Dictionary<string, decimal> TotalByProject,
    Dictionary<string, decimal> TotalByProvider,
    int TransactionCount,
    int RefundCount,
    string BaseCurrency = "USD");

/// <summary>
/// Merchant-specific report DTO.
/// </summary>
public sealed record MerchantReportDto(
    string MerchantId,
    DateTime From,
    DateTime To,
    decimal TotalProcessed,
    decimal TotalRefunded,
    decimal NetRevenue,
    int TransactionCount,
    int RefundCount,
    decimal AverageTransactionAmount,
    decimal RefundRate,
    Dictionary<string, decimal> TotalByProvider,
    string BaseCurrency = "USD");

/// <summary>
/// Provider-specific report DTO.
/// </summary>
public sealed record ProviderReportDto(
    string Provider,
    DateTime From,
    DateTime To,
    decimal TotalProcessed,
    decimal TotalRefunded,
    int TransactionCount,
    int SuccessfulTransactionCount,
    int FailedTransactionCount,
    decimal SuccessRate,
    decimal AverageTransactionAmount,
    decimal RefundRate,
    Dictionary<string, decimal> TotalByProject,
    string BaseCurrency = "USD");

/// <summary>
/// Refund report DTO.
/// </summary>
public sealed record RefundReportDto(
    DateTime From,
    DateTime To,
    decimal TotalRefunded,
    int RefundCount,
    Dictionary<string, int> RefundsByReason,
    Dictionary<string, decimal> RefundsByProject,
    Dictionary<string, decimal> RefundsByProvider,
    string BaseCurrency = "USD");

/// <summary>
/// Complete dispute trace DTO with full audit trail.
/// </summary>
public sealed record DisputeTraceDto(
    Guid PaymentId,
    string OrderId,
    string MerchantId,
    string? ProjectCode,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    DateTime CreatedAt,
    DateTime? RefundedAt,
    IReadOnlyList<PaymentSplitDto> Splits,
    IReadOnlyList<RefundDto> Refunds,
    IReadOnlyList<RefundAuditLogDto> AuditLogs);

/// <summary>
/// Payment split DTO for dispute trace.
/// </summary>
public sealed record PaymentSplitDto(
    Guid Id,
    string AccountType,
    string AccountIdentifier,
    decimal Percentage,
    decimal Amount);

/// <summary>
/// Refund DTO for dispute trace.
/// </summary>
public sealed record RefundDto(
    Guid Id,
    decimal Amount,
    string Currency,
    string Reason,
    PaymentStatus Status,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

/// <summary>
/// Refund audit log DTO for dispute trace.
/// </summary>
public sealed record RefundAuditLogDto(
    Guid Id,
    string Action,
    string PerformedBy,
    string? Reason,
    DateTime Timestamp);

/// <summary>
/// Real-time statistics DTO.
/// </summary>
public sealed record RealTimeStatsDto(
    DateTime From,
    DateTime To,
    decimal TotalProcessed,
    int TransactionCount,
    Dictionary<PaymentStatus, int> StatusCounts,
    Dictionary<string, decimal> TotalByProject,
    Dictionary<string, decimal> TotalByProvider,
    string BaseCurrency = "USD");

/// <summary>
/// Revenue forecast DTO.
/// </summary>
public sealed record ForecastReportDto(
    DateTime From,
    DateTime To,
    decimal ForecastedRevenue,
    decimal ConfidenceLower,
    decimal ConfidenceUpper,
    IReadOnlyList<ForecastPeriodDto> Periods,
    string BaseCurrency = "USD");

/// <summary>
/// Forecast period DTO.
/// </summary>
public sealed record ForecastPeriodDto(
    DateTime Period,
    decimal ForecastedAmount,
    decimal ConfidenceLower,
    decimal ConfidenceUpper);

/// <summary>
/// Anomaly alert DTO.
/// </summary>
public sealed record AnomalyAlertDto(
    DateTime DetectedAt,
    string Type,
    string Severity,
    string Description,
    decimal? ExpectedValue,
    decimal? ActualValue,
    string? ProjectCode,
    Dictionary<string, object>? Metadata);

