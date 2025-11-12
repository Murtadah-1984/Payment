using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

public class PaymentIdTests
{
    [Fact]
    public void NewId_ShouldCreateNewGuid()
    {
        // Act
        var id1 = PaymentId.NewId();
        var id2 = PaymentId.NewId();

        // Assert
        id1.Value.Should().NotBeEmpty();
        id2.Value.Should().NotBeEmpty();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void FromGuid_ShouldCreatePaymentId_FromGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = PaymentId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void PaymentId_ShouldBeEqual_WhenValuesAreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = PaymentId.FromGuid(guid);
        var id2 = PaymentId.FromGuid(guid);

        // Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }
}

