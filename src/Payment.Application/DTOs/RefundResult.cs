using Payment.Domain.ValueObjects;

namespace Payment.Application.DTOs;

/// <summary>
/// Result DTO for refund operations.
/// </summary>
public sealed record RefundResult(
    Dictionary<PaymentId, bool> RefundStatuses,
    int TotalProcessed,
    int Successful,
    int Failed,
    IEnumerable<string> Errors)
{
    public static RefundResult Create(
        Dictionary<PaymentId, bool> refundStatuses,
        IEnumerable<string>? errors = null)
    {
        var successful = refundStatuses.Values.Count(v => v);
        var failed = refundStatuses.Values.Count(v => !v);

        return new RefundResult(
            RefundStatuses: refundStatuses,
            TotalProcessed: refundStatuses.Count,
            Successful: successful,
            Failed: failed,
            Errors: errors ?? Enumerable.Empty<string>());
    }
}

