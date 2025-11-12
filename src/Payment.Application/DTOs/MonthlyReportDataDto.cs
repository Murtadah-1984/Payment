namespace Payment.Application.DTOs;

/// <summary>
/// DTO for monthly report data used in report generation.
/// </summary>
public sealed record MonthlyReportDataDto(
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
    int FailedTransactionCount,
    DateTime GeneratedAtUtc);

