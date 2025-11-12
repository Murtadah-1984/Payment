using FluentAssertions;
using Payment.Application.Services;
using Xunit;

namespace Payment.Application.Tests.Services;

public class SplitPaymentServiceTests
{
    private readonly ISplitPaymentService _service;

    public SplitPaymentServiceTests()
    {
        _service = new SplitPaymentService();
    }

    [Fact]
    public void CalculateSplit_ShouldCalculateCorrectly_WhenSystemFeeIs5Percent()
    {
        // Arrange
        var totalAmount = 100.00m;
        var systemFeePercent = 5.0m;

        // Act
        var result = _service.CalculateSplit(totalAmount, systemFeePercent);

        // Assert
        result.SystemShare.Should().Be(5.00m);
        result.OwnerShare.Should().Be(95.00m);
        result.SystemFeePercent.Should().Be(5.0m);
        result.TotalAmount.Should().Be(100.00m);
    }

    [Fact]
    public void CalculateSplit_ShouldCalculateCorrectly_WhenSystemFeeIs10Percent()
    {
        // Arrange
        var totalAmount = 1000.00m;
        var systemFeePercent = 10.0m;

        // Act
        var result = _service.CalculateSplit(totalAmount, systemFeePercent);

        // Assert
        result.SystemShare.Should().Be(100.00m);
        result.OwnerShare.Should().Be(900.00m);
        result.TotalAmount.Should().Be(1000.00m);
    }

    [Fact]
    public void CalculateSplit_ShouldRoundToTwoDecimals()
    {
        // Arrange
        var totalAmount = 99.99m;
        var systemFeePercent = 5.0m;

        // Act
        var result = _service.CalculateSplit(totalAmount, systemFeePercent);

        // Assert
        result.SystemShare.Should().Be(5.00m);
        result.OwnerShare.Should().Be(94.99m);
        result.TotalAmount.Should().Be(99.99m);
    }

    [Fact]
    public void CalculateSplit_ShouldThrowException_WhenAmountIsZero()
    {
        // Arrange
        var totalAmount = 0m;
        var systemFeePercent = 5.0m;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.CalculateSplit(totalAmount, systemFeePercent));
    }

    [Fact]
    public void CalculateSplit_ShouldThrowException_WhenFeePercentIsNegative()
    {
        // Arrange
        var totalAmount = 100m;
        var systemFeePercent = -5.0m;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.CalculateSplit(totalAmount, systemFeePercent));
    }

    [Fact]
    public void CalculateSplit_ShouldThrowException_WhenFeePercentIsOver100()
    {
        // Arrange
        var totalAmount = 100m;
        var systemFeePercent = 101.0m;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.CalculateSplit(totalAmount, systemFeePercent));
    }
}

