using FluentAssertions;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for FraudCheckResult value object (Fraud Detection #22).
/// </summary>
public class FraudCheckResultTests
{
    [Fact]
    public void LowRisk_ShouldCreateLowRiskResult()
    {
        // Act
        var result = FraudCheckResult.LowRisk();

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Low);
        result.Recommendation.Should().Be("Approve");
        result.RiskScore.Should().Be(0.0m);
        result.Reasons.Should().BeEmpty();
        result.IsLowRisk.Should().BeTrue();
        result.IsMediumRisk.Should().BeFalse();
        result.IsHighRisk.Should().BeFalse();
        result.ShouldBlock.Should().BeFalse();
        result.ShouldReview.Should().BeFalse();
    }

    [Fact]
    public void LowRisk_ShouldIncludeTransactionId_WhenProvided()
    {
        // Arrange
        var transactionId = "txn-123";

        // Act
        var result = FraudCheckResult.LowRisk(transactionId);

        // Assert
        result.TransactionId.Should().Be(transactionId);
    }

    [Fact]
    public void MediumRisk_ShouldCreateMediumRiskResult()
    {
        // Arrange
        var riskScore = 0.65m;
        var reasons = new[] { "Unusual amount", "New device" };

        // Act
        var result = FraudCheckResult.MediumRisk(riskScore, reasons);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.Medium);
        result.Recommendation.Should().Be("Review");
        result.RiskScore.Should().Be(riskScore);
        result.Reasons.Should().BeEquivalentTo(reasons);
        result.IsLowRisk.Should().BeFalse();
        result.IsMediumRisk.Should().BeTrue();
        result.IsHighRisk.Should().BeFalse();
        result.ShouldBlock.Should().BeFalse();
        result.ShouldReview.Should().BeTrue();
    }

    [Fact]
    public void MediumRisk_ShouldIncludeTransactionId_WhenProvided()
    {
        // Arrange
        var riskScore = 0.65m;
        var reasons = new[] { "Unusual amount" };
        var transactionId = "txn-456";

        // Act
        var result = FraudCheckResult.MediumRisk(riskScore, reasons, transactionId);

        // Assert
        result.TransactionId.Should().Be(transactionId);
    }

    [Fact]
    public void HighRisk_ShouldCreateHighRiskResult()
    {
        // Arrange
        var riskScore = 0.95m;
        var reasons = new[] { "Suspicious IP", "Multiple failed attempts", "Velocity check failed" };

        // Act
        var result = FraudCheckResult.HighRisk(riskScore, reasons);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(FraudRiskLevel.High);
        result.Recommendation.Should().Be("Block");
        result.RiskScore.Should().Be(riskScore);
        result.Reasons.Should().BeEquivalentTo(reasons);
        result.IsLowRisk.Should().BeFalse();
        result.IsMediumRisk.Should().BeFalse();
        result.IsHighRisk.Should().BeTrue();
        result.ShouldBlock.Should().BeTrue();
        result.ShouldReview.Should().BeFalse();
    }

    [Fact]
    public void HighRisk_ShouldIncludeTransactionId_WhenProvided()
    {
        // Arrange
        var riskScore = 0.95m;
        var reasons = new[] { "Suspicious activity" };
        var transactionId = "txn-789";

        // Act
        var result = FraudCheckResult.HighRisk(riskScore, reasons, transactionId);

        // Assert
        result.TransactionId.Should().Be(transactionId);
    }

    [Fact]
    public void Create_ShouldCreateResult_WhenAllParametersProvided()
    {
        // Arrange
        var riskLevel = FraudRiskLevel.Medium;
        var recommendation = "Review";
        var riskScore = 0.75m;
        var reasons = new[] { "Reason 1", "Reason 2" };
        var transactionId = "txn-999";

        // Act
        var result = new FraudCheckResult(riskLevel, recommendation, riskScore, reasons, transactionId);

        // Assert
        result.Should().NotBeNull();
        result.RiskLevel.Should().Be(riskLevel);
        result.Recommendation.Should().Be(recommendation);
        result.RiskScore.Should().Be(riskScore);
        result.Reasons.Should().BeEquivalentTo(reasons);
        result.TransactionId.Should().Be(transactionId);
    }
}

