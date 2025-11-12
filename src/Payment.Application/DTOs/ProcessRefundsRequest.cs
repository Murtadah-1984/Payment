using Payment.Domain.ValueObjects;

namespace Payment.Application.DTOs;

/// <summary>
/// Request DTO for processing refunds.
/// </summary>
public sealed record ProcessRefundsRequest(
    IEnumerable<Guid> PaymentIds,
    string? Reason = null)
{
    public IEnumerable<PaymentId> ToPaymentIds()
    {
        return PaymentIds.Select(id => PaymentId.FromGuid(id));
    }
}

