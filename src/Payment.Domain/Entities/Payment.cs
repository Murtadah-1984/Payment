using Payment.Domain.Enums;
using Payment.Domain.Events;
using Payment.Domain.ValueObjects;
using Payment.Domain.Services;

namespace Payment.Domain.Entities;

public class Payment : Entity
{
    private Payment() { } // EF Core

    public Payment(
        PaymentId id,
        Amount amount,
        Currency currency,
        PaymentMethod paymentMethod,
        PaymentProvider provider,
        string merchantId,
        string orderId,
        SplitPayment? splitPayment = null,
        Dictionary<string, string>? metadata = null,
        PaymentStatus status = PaymentStatus.Pending,
        CardToken? cardToken = null,
        string? projectCode = null,
        decimal? systemFeeAmount = null)
    {
        Id = id;
        Amount = amount;
        Currency = currency;
        PaymentMethod = paymentMethod;
        Provider = provider;
        MerchantId = merchantId;
        OrderId = orderId;
        SplitPayment = splitPayment;
        Metadata = metadata ?? new Dictionary<string, string>();
        Status = status;
        CardToken = cardToken;
        ProjectCode = projectCode ?? (metadata?.ContainsKey("project_code") == true ? metadata["project_code"] : null);
        SystemFeeAmount = systemFeeAmount ?? (splitPayment?.SystemShare ?? 0m);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public PaymentId Id { get; private set; }
    public Amount Amount { get; private set; }
    public Currency Currency { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentProvider Provider { get; private set; }
    public string MerchantId { get; private set; } = string.Empty;
    public string OrderId { get; private set; } = string.Empty;
    public SplitPayment? SplitPayment { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();
    public PaymentStatus Status { get; private set; }
    public string? TransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public CardToken? CardToken { get; private set; }
    public string? ProjectCode { get; private set; }
    public decimal SystemFeeAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    
    // Multi-Currency Settlement (LOW #21)
    public Currency? SettlementCurrency { get; private set; }
    public decimal? SettlementAmount { get; private set; }
    public decimal? ExchangeRate { get; private set; }
    public DateTime? SettledAt { get; private set; }

    // 3D Secure Support (LOW #23)
    public ThreeDSecureStatus ThreeDSecureStatus { get; private set; } = ThreeDSecureStatus.NotRequired;
    public string? ThreeDSecureCavv { get; private set; }
    public string? ThreeDSecureEci { get; private set; }
    public string? ThreeDSecureXid { get; private set; }
    public string? ThreeDSecureVersion { get; private set; }

    /// <summary>
    /// Processes the payment using state machine validation.
    /// </summary>
    /// <param name="transactionId">The transaction ID from the payment provider.</param>
    /// <param name="stateService">The state service to validate transitions (State Machine #18).</param>
    public void Process(string transactionId, IPaymentStateService stateService)
    {
        if (stateService == null)
            throw new ArgumentNullException(nameof(stateService));

        // Use state machine to validate and transition
        Status = stateService.Transition(Status, PaymentTrigger.Process);
        TransactionId = transactionId;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentProcessingEvent(Id.Value, OrderId));
    }

    /// <summary>
    /// Completes the payment using state machine validation.
    /// </summary>
    /// <param name="stateService">The state service to validate transitions (State Machine #18).</param>
    public void Complete(IPaymentStateService stateService)
    {
        if (stateService == null)
            throw new ArgumentNullException(nameof(stateService));

        // Use state machine to validate and transition
        Status = stateService.Transition(Status, PaymentTrigger.Complete);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentCompletedEvent(Id.Value, OrderId, Amount.Value, Currency.Code));
    }

    /// <summary>
    /// Fails the payment using state machine validation.
    /// </summary>
    /// <param name="reason">The reason for failure.</param>
    /// <param name="stateService">The state service to validate transitions (State Machine #18).</param>
    public void Fail(string reason, IPaymentStateService stateService)
    {
        if (stateService == null)
            throw new ArgumentNullException(nameof(stateService));

        // Use state machine to validate and transition
        Status = stateService.Transition(Status, PaymentTrigger.Fail);
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentFailedEvent(Id.Value, OrderId, reason));
    }

    /// <summary>
    /// Refunds the payment using state machine validation.
    /// </summary>
    /// <param name="refundTransactionId">The refund transaction ID.</param>
    /// <param name="stateService">The state service to validate transitions (State Machine #18).</param>
    public void Refund(string refundTransactionId, IPaymentStateService stateService)
    {
        if (stateService == null)
            throw new ArgumentNullException(nameof(stateService));

        // Use state machine to validate and transition
        Status = stateService.Transition(Status, PaymentTrigger.Refund);
        TransactionId = refundTransactionId;
        RefundedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentRefundedEvent(Id.Value, OrderId, Amount.Value, Currency.Code));
    }

    /// <summary>
    /// Partially refunds the payment using state machine validation.
    /// </summary>
    /// <param name="refundAmount">The amount to refund.</param>
    /// <param name="stateService">The state service to validate transitions (State Machine #18).</param>
    public void PartiallyRefund(decimal refundAmount, IPaymentStateService stateService)
    {
        if (stateService == null)
            throw new ArgumentNullException(nameof(stateService));

        if (refundAmount <= 0 || refundAmount >= Amount.Value)
        {
            throw new ArgumentException("Refund amount must be greater than zero and less than payment amount", nameof(refundAmount));
        }

        // Use state machine to validate and transition
        Status = stateService.Transition(Status, PaymentTrigger.PartialRefund);
        RefundedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the card token for this payment (PCI DSS compliance - tokenization).
    /// Should only be called when payment provider returns a tokenized card.
    /// </summary>
    public void SetCardToken(CardToken cardToken)
    {
        if (cardToken == null)
            throw new ArgumentNullException(nameof(cardToken));

        CardToken = cardToken;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the settlement currency and amount for multi-currency settlement.
    /// Called automatically when payment is completed if settlement currency differs from payment currency.
    /// </summary>
    /// <param name="settlementCurrency">The currency in which the payment is settled.</param>
    /// <param name="settlementAmount">The amount in settlement currency.</param>
    /// <param name="exchangeRate">The exchange rate used for conversion.</param>
    public void SetSettlement(Currency settlementCurrency, decimal settlementAmount, decimal exchangeRate)
    {
        if (settlementCurrency == null)
            throw new ArgumentNullException(nameof(settlementCurrency));

        if (settlementAmount < 0)
            throw new ArgumentException("Settlement amount cannot be negative", nameof(settlementAmount));

        if (exchangeRate <= 0)
            throw new ArgumentException("Exchange rate must be positive", nameof(exchangeRate));

        SettlementCurrency = settlementCurrency;
        SettlementAmount = settlementAmount;
        ExchangeRate = exchangeRate;
        SettledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Initiates 3D Secure authentication for this payment.
    /// Sets the payment status to indicate 3DS challenge is required.
    /// </summary>
    /// <param name="challenge">The 3DS challenge details</param>
    public void InitiateThreeDSecure(ThreeDSecureChallenge challenge)
    {
        if (challenge == null)
            throw new ArgumentNullException(nameof(challenge));

        ThreeDSecureStatus = ThreeDSecureStatus.ChallengeRequired;
        Metadata["3ds_acs_url"] = challenge.AcsUrl;
        Metadata["3ds_pareq"] = challenge.Pareq;
        Metadata["3ds_md"] = challenge.Md;
        Metadata["3ds_term_url"] = challenge.TermUrl;
        Metadata["3ds_version"] = challenge.Version;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Completes 3D Secure authentication for this payment.
    /// Updates the payment with 3DS authentication result.
    /// </summary>
    /// <param name="result">The 3DS authentication result</param>
    public void CompleteThreeDSecure(ThreeDSecureResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        if (result.Authenticated)
        {
            ThreeDSecureStatus = ThreeDSecureStatus.Authenticated;
            ThreeDSecureCavv = result.Cavv;
            ThreeDSecureEci = result.Eci;
            ThreeDSecureXid = result.Xid;
            ThreeDSecureVersion = result.Version;

            if (!string.IsNullOrEmpty(result.Cavv))
                Metadata["3ds_cavv"] = result.Cavv;
            if (!string.IsNullOrEmpty(result.Eci))
                Metadata["3ds_eci"] = result.Eci;
            if (!string.IsNullOrEmpty(result.Xid))
                Metadata["3ds_xid"] = result.Xid;
        }
        else
        {
            ThreeDSecureStatus = ThreeDSecureStatus.Failed;
            FailureReason = result.FailureReason ?? "3D Secure authentication failed";
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks 3D Secure as not required for this payment.
    /// </summary>
    public void SkipThreeDSecure()
    {
        ThreeDSecureStatus = ThreeDSecureStatus.Skipped;
        UpdatedAt = DateTime.UtcNow;
    }
}

