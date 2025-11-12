namespace Payment.Domain.ValueObjects;

public sealed record SplitPayment(
    decimal SystemShare,
    decimal OwnerShare,
    decimal SystemFeePercent)
{
    public static SplitPayment Calculate(decimal totalAmount, decimal systemFeePercent)
    {
        if (totalAmount <= 0)
        {
            throw new ArgumentException("Total amount must be greater than zero", nameof(totalAmount));
        }

        if (systemFeePercent < 0 || systemFeePercent > 100)
        {
            throw new ArgumentException("System fee percent must be between 0 and 100", nameof(systemFeePercent));
        }

        var systemShare = Math.Round(totalAmount * systemFeePercent / 100, 2);
        var ownerShare = Math.Round(totalAmount - systemShare, 2);

        return new SplitPayment(systemShare, ownerShare, systemFeePercent);
    }

    public decimal TotalAmount => SystemShare + OwnerShare;
}

