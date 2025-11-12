using FluentAssertions;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for FraudCheckRequest value object (Fraud Detection #22).
/// </summary>
public class FraudCheckRequestTests
{
    [Fact]
    public void Create_ShouldCreateRequest_WhenValidParameters()
    {
        // Arrange & Act
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        // Assert
        request.Should().NotBeNull();
        request.Amount.Should().Be(100.50m);
        request.Currency.Should().Be("USD");
        request.PaymentMethod.Should().Be("CreditCard");
        request.MerchantId.Should().Be("merchant-123");
        request.OrderId.Should().Be("order-456");
    }

    [Fact]
    public void Create_ShouldCreateRequest_WhenAllOptionalParametersProvided()
    {
        // Arrange & Act
        var metadata = new Dictionary<string, string> { { "key", "value" } };
        var request = FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456",
            customerEmail: "customer@example.com",
            customerPhone: "+1234567890",
            customerId: "customer-123",
            deviceId: "device-456",
            ipAddress: "192.168.1.1",
            projectCode: "PROJECT-001",
            metadata: metadata);

        // Assert
        request.Should().NotBeNull();
        request.CustomerEmail.Should().Be("customer@example.com");
        request.CustomerPhone.Should().Be("+1234567890");
        request.CustomerId.Should().Be("customer-123");
        request.DeviceId.Should().Be("device-456");
        request.IpAddress.Should().Be("192.168.1.1");
        request.ProjectCode.Should().Be("PROJECT-001");
        request.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void Create_ShouldThrowException_WhenAmountIsZeroOrNegative(decimal amount)
    {
        // Act & Assert
        var action = () => FraudCheckRequest.Create(
            amount: amount,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Amount must be greater than zero*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowException_WhenCurrencyIsNullOrEmpty(string? currency)
    {
        // Act & Assert
        var action = () => FraudCheckRequest.Create(
            amount: 100.50m,
            currency: currency!,
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: "order-456");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Currency cannot be null or empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowException_WhenPaymentMethodIsNullOrEmpty(string? paymentMethod)
    {
        // Act & Assert
        var action = () => FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: paymentMethod!,
            merchantId: "merchant-123",
            orderId: "order-456");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Payment method cannot be null or empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowException_WhenMerchantIdIsNullOrEmpty(string? merchantId)
    {
        // Act & Assert
        var action = () => FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: merchantId!,
            orderId: "order-456");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Merchant ID cannot be null or empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowException_WhenOrderIdIsNullOrEmpty(string? orderId)
    {
        // Act & Assert
        var action = () => FraudCheckRequest.Create(
            amount: 100.50m,
            currency: "USD",
            paymentMethod: "CreditCard",
            merchantId: "merchant-123",
            orderId: orderId!);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Order ID cannot be null or empty*");
    }
}

