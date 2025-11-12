using Payment.Application.DTOs;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Services;

public interface ISplitPaymentService
{
    /// <summary>
    /// Calculates a simple 2-party split (system and owner).
    /// </summary>
    SplitPayment CalculateSplit(decimal totalAmount, decimal systemFeePercent);

    /// <summary>
    /// Calculates a multi-account split based on a split rule.
    /// Returns the split payment value object and account details for storage in metadata.
    /// </summary>
    (SplitPayment SplitPayment, Dictionary<string, object> SplitDetails) CalculateMultiAccountSplit(
        decimal totalAmount, 
        SplitRuleDto splitRule);
}

