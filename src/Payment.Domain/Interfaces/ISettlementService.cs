namespace Payment.Domain.Interfaces;

/// <summary>
/// Service for handling multi-currency settlement.
/// Automatically converts payment amounts to settlement currency when payment is completed.
/// </summary>
public interface ISettlementService
{
    /// <summary>
    /// Processes settlement for a payment, converting to settlement currency if needed.
    /// </summary>
    /// <param name="payment">The payment to settle.</param>
    /// <param name="settlementCurrency">The target currency for settlement (e.g., USD).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if settlement was processed (conversion occurred), false if no conversion needed.</returns>
    Task<bool> ProcessSettlementAsync(
        Entities.Payment payment,
        string settlementCurrency,
        CancellationToken cancellationToken = default);
}

