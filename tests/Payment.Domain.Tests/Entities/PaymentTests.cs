using FluentAssertions;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Events;
using Payment.Domain.ValueObjects;
using Payment.Domain.Tests.Helpers;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Domain.Tests.Entities;

public class PaymentTests
{
    [Fact]
    public void Constructor_ShouldCreatePayment_WithValidParameters()
    {
        // Arrange
        var id = PaymentId.NewId();
        var amount = Amount.FromDecimal(100.50m);
        var currency = Currency.USD;
        var paymentMethod = PaymentMethod.CreditCard;
        var provider = PaymentProvider.ZainCash;
        var merchantId = "merchant-123";
        var orderId = "order-456";

        // Act
        var payment = new PaymentEntity(id, amount, currency, paymentMethod, provider, merchantId, orderId);

        // Assert
        payment.Id.Should().Be(id);
        payment.Amount.Should().Be(amount);
        payment.Currency.Should().Be(currency);
        payment.PaymentMethod.Should().Be(paymentMethod);
        payment.Provider.Should().Be(provider);
        payment.MerchantId.Should().Be(merchantId);
        payment.OrderId.Should().Be(orderId);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.TransactionId.Should().BeNull();
        payment.FailureReason.Should().BeNull();
        payment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        payment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Process_ShouldChangeStatusToProcessing_WhenStatusIsPending()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();
        var transactionId = "txn-123";

        // Act
        payment.Process(transactionId, stateService);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Processing);
        payment.TransactionId.Should().Be(transactionId);
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentProcessingEvent);
    }

    [Fact]
    public void Process_ShouldThrowException_WhenStatusIsNotPending()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();
        payment.Process("txn-123", stateService);
        payment.ClearDomainEvents();

        // Act & Assert
        var act = () => payment.Process("txn-456", stateService);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot transition from {PaymentStatus.Processing} using trigger {PaymentTrigger.Process}");
    }

    [Fact]
    public void Complete_ShouldChangeStatusToSucceeded_WhenStatusIsProcessing()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();
        payment.Process("txn-123", stateService);
        payment.ClearDomainEvents();

        // Act
        payment.Complete(stateService);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentCompletedEvent);
    }

    [Fact]
    public void Complete_ShouldThrowException_WhenStatusIsNotProcessing()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();

        // Act & Assert
        var act = () => payment.Complete(stateService);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot transition from {PaymentStatus.Pending} using trigger {PaymentTrigger.Complete}");
    }

    [Fact]
    public void Fail_ShouldChangeStatusToFailed_WhenStatusIsPending()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();
        var reason = "Insufficient funds";

        // Act
        payment.Fail(reason, stateService);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be(reason);
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentFailedEvent);
    }

    [Fact]
    public void Fail_ShouldThrowException_WhenStatusIsSucceeded()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();
        payment.Process("txn-123", stateService);
        payment.Complete(stateService);
        payment.ClearDomainEvents();

        // Act & Assert
        var act = () => payment.Fail("Some reason", stateService);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot transition from {PaymentStatus.Succeeded} using trigger {PaymentTrigger.Fail}");
    }

    [Fact]
    public void Refund_ShouldChangeStatusToRefunded_WhenStatusIsSucceeded()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();
        payment.Process("txn-123", stateService);
        payment.Complete(stateService);
        payment.ClearDomainEvents();
        var refundTransactionId = "refund-123";

        // Act
        payment.Refund(refundTransactionId, stateService);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.TransactionId.Should().Be(refundTransactionId);
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentRefundedEvent);
    }

    [Fact]
    public void Refund_ShouldThrowException_WhenStatusIsNotSucceeded()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();
        payment.Process("txn-123", stateService);

        // Act & Assert
        var act = () => payment.Refund("refund-123", stateService);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot transition from {PaymentStatus.Processing} using trigger {PaymentTrigger.Refund}");
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllDomainEvents()
    {
        // Arrange
        var payment = CreateTestPayment();
        var stateService = MockPaymentStateService.CreateWithValidation();
        payment.Process("txn-123", stateService);

        // Act
        payment.ClearDomainEvents();

        // Assert
        payment.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ShouldInitializeMetadata_WhenProvided()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456",
            metadata: metadata);

        // Assert
        payment.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void Constructor_ShouldInitializeSplitPayment_WhenProvided()
    {
        // Arrange
        var splitPayment = SplitPayment.Calculate(100m, 5m);
        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456",
            splitPayment: splitPayment);

        // Assert
        payment.SplitPayment.Should().Be(splitPayment);
    }

    [Fact]
    public void Constructor_ShouldInitializeCardToken_WhenProvided()
    {
        // Arrange
        var cardToken = new CardToken("token_1234567890", "1234", "Visa");
        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456",
            cardToken: cardToken);

        // Assert
        payment.CardToken.Should().Be(cardToken);
        payment.CardToken!.Token.Should().Be("token_1234567890");
        payment.CardToken.Last4Digits.Should().Be("1234");
        payment.CardToken.CardBrand.Should().Be("Visa");
    }

    [Fact]
    public void Constructor_ShouldNotRequireCardToken_WhenNotProvided()
    {
        // Arrange & Act
        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100m),
            Currency.USD,
            PaymentMethod.PayPal,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");

        // Assert
        payment.CardToken.Should().BeNull();
    }

    [Fact]
    public void SetCardToken_ShouldSetCardToken_WhenValidTokenIsProvided()
    {
        // Arrange
        var payment = CreateTestPayment();
        var cardToken = new CardToken("token_1234567890", "1234", "Visa");
        var originalUpdatedAt = payment.UpdatedAt;

        // Act
        payment.SetCardToken(cardToken);

        // Assert
        payment.CardToken.Should().Be(cardToken);
        payment.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void SetCardToken_ShouldThrowException_WhenCardTokenIsNull()
    {
        // Arrange
        var payment = CreateTestPayment();

        // Act & Assert
        var act = () => payment.SetCardToken(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cardToken");
    }

    private static Payment.Domain.Entities.Payment CreateTestPayment()
    {
        return new Payment.Domain.Entities.Payment(
            PaymentId.NewId(),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");
    }
}

