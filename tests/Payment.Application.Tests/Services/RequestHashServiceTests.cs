using FluentAssertions;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Xunit;

namespace Payment.Application.Tests.Services;

public class RequestHashServiceTests
{
    private readonly IRequestHashService _requestHashService;

    public RequestHashServiceTests()
    {
        _requestHashService = new RequestHashService();
    }

    [Fact]
    public void ComputeRequestHash_WithSameRequest_ShouldReturnSameHash()
    {
        // Arrange
        var request1 = new CreatePaymentDto(
            RequestId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard",
            Provider: "Stripe",
            MerchantId: "merchant-123",
            OrderId: "order-456",
            ProjectCode: "PROJECT-XYZ",
            IdempotencyKey: "idempotency-key-123",
            SystemFeePercent: 5.0m,
            Metadata: new Dictionary<string, string> { { "key1", "value1" } });

        var request2 = new CreatePaymentDto(
            RequestId: request1.RequestId, // Same RequestId
            Amount: request1.Amount,
            Currency: request1.Currency,
            PaymentMethod: request1.PaymentMethod,
            Provider: request1.Provider,
            MerchantId: request1.MerchantId,
            OrderId: request1.OrderId,
            ProjectCode: request1.ProjectCode,
            IdempotencyKey: request1.IdempotencyKey,
            SystemFeePercent: request1.SystemFeePercent,
            Metadata: request1.Metadata);

        // Act
        var hash1 = _requestHashService.ComputeRequestHash(request1);
        var hash2 = _requestHashService.ComputeRequestHash(request2);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64); // SHA-256 produces 64-character hex string
    }

    [Fact]
    public void ComputeRequestHash_WithDifferentAmount_ShouldReturnDifferentHash()
    {
        // Arrange
        var request1 = new CreatePaymentDto(
            RequestId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard",
            Provider: "Stripe",
            MerchantId: "merchant-123",
            OrderId: "order-456",
            ProjectCode: "PROJECT-XYZ",
            IdempotencyKey: "idempotency-key-123");

        var request2 = request1 with { Amount = 200.00m };

        // Act
        var hash1 = _requestHashService.ComputeRequestHash(request1);
        var hash2 = _requestHashService.ComputeRequestHash(request2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeRequestHash_WithDifferentMetadataOrder_ShouldReturnSameHash()
    {
        // Arrange
        var request1 = new CreatePaymentDto(
            RequestId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard",
            Provider: "Stripe",
            MerchantId: "merchant-123",
            OrderId: "order-456",
            ProjectCode: "PROJECT-XYZ",
            IdempotencyKey: "idempotency-key-123",
            Metadata: new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            });

        var request2 = request1 with
        {
            Metadata = new Dictionary<string, string>
            {
                { "key2", "value2" }, // Different order
                { "key1", "value1" }
            }
        };

        // Act
        var hash1 = _requestHashService.ComputeRequestHash(request1);
        var hash2 = _requestHashService.ComputeRequestHash(request2);

        // Assert
        hash1.Should().Be(hash2); // Should be same because metadata is sorted
    }

    [Fact]
    public void ComputeRequestHash_WithNullMetadata_ShouldReturnValidHash()
    {
        // Arrange
        var request = new CreatePaymentDto(
            RequestId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard",
            Provider: "Stripe",
            MerchantId: "merchant-123",
            OrderId: "order-456",
            ProjectCode: "PROJECT-XYZ",
            IdempotencyKey: "idempotency-key-123",
            Metadata: null);

        // Act
        var hash = _requestHashService.ComputeRequestHash(request);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64);
    }
}


