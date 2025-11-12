using FluentAssertions;
using Microsoft.Extensions.Logging;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Services;
using Payment.Domain.Tests.Helpers;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Domain.Tests.Services;

public class RegulatoryRulesEngineTests
{
    private readonly ILogger<RegulatoryRulesEngine> _logger;

    public RegulatoryRulesEngineTests()
    {
        _logger = new LoggerFactory().CreateLogger<RegulatoryRulesEngine>();
    }

    [Fact]
    public void GetRule_ShouldReturnRule_WhenCountryCodeExists()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true),
            new("SA", "SAMA", "Saudi Arabian Monetary Authority requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);

        // Act
        var rule = engine.GetRule("KW");

        // Assert
        rule.Should().NotBeNull();
        rule!.CountryCode.Should().Be("KW");
        rule.RegulationName.Should().Be("CBK");
        rule.Requires3DSecure.Should().BeTrue();
    }

    [Fact]
    public void GetRule_ShouldReturnNull_WhenCountryCodeDoesNotExist()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);

        // Act
        var rule = engine.GetRule("US");

        // Assert
        rule.Should().BeNull();
    }

    [Fact]
    public void GetRule_ShouldBeCaseInsensitive()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);

        // Act
        var rule1 = engine.GetRule("kw");
        var rule2 = engine.GetRule("Kw");
        var rule3 = engine.GetRule("KW");

        // Assert
        rule1.Should().NotBeNull();
        rule2.Should().NotBeNull();
        rule3.Should().NotBeNull();
        rule1!.CountryCode.Should().Be("KW");
        rule2!.CountryCode.Should().Be("KW");
        rule3!.CountryCode.Should().Be("KW");
    }

    [Fact]
    public void GetRule_ShouldReturnNull_WhenCountryCodeIsNullOrEmpty()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);

        // Act
        var rule1 = engine.GetRule(null!);
        var rule2 = engine.GetRule(string.Empty);
        var rule3 = engine.GetRule("   ");

        // Assert
        rule1.Should().BeNull();
        rule2.Should().BeNull();
        rule3.Should().BeNull();
    }

    [Fact]
    public void ValidateTransaction_ShouldReturnTrue_WhenNoRuleExists()
    {
        // Arrange
        var rules = new List<ComplianceRule>();
        var engine = new RegulatoryRulesEngine(rules, _logger);
        var payment = CreateTestPayment();

        // Act
        var result = engine.ValidateTransaction("US", payment);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateTransaction_ShouldReturnTrue_When3DSecureIsRequiredAndAuthenticated()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);
        var payment = CreateTestPayment();
        payment.CompleteThreeDSecure(new Payment.Domain.ValueObjects.ThreeDSecureResult(
            authenticated: true,
            cavv: "test-cavv",
            eci: "05",
            xid: null,
            version: "2.1.0",
            failureReason: null));

        // Act
        var result = engine.ValidateTransaction("KW", payment);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateTransaction_ShouldReturnTrue_When3DSecureIsRequiredAndSkipped()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);
        var payment = CreateTestPayment();
        payment.SkipThreeDSecure();

        // Act
        var result = engine.ValidateTransaction("KW", payment);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateTransaction_ShouldReturnFalse_When3DSecureIsRequiredButNotAuthenticated()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);
        var payment = CreateTestPayment();
        // Payment has ThreeDSecureStatus = NotRequired by default

        // Act
        var result = engine.ValidateTransaction("KW", payment);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateTransaction_ShouldReturnFalse_When3DSecureIsRequiredButFailed()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);
        var payment = CreateTestPayment();
        payment.CompleteThreeDSecure(new Payment.Domain.ValueObjects.ThreeDSecureResult(
            authenticated: false,
            cavv: null,
            eci: null,
            xid: null,
            version: "2.1.0",
            failureReason: "Authentication failed"));

        // Act
        var result = engine.ValidateTransaction("KW", payment);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateTransaction_ShouldReturnTrue_When3DSecureIsNotRequired()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("IQ", "CBI", "Central Bank of Iraq requirements", false, true, false)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);
        var payment = CreateTestPayment();
        // Payment has ThreeDSecureStatus = NotRequired by default

        // Act
        var result = engine.ValidateTransaction("IQ", payment);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateTransaction_ShouldThrowArgumentNullException_WhenPaymentIsNull()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);

        // Act
        var act = () => engine.ValidateTransaction("KW", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("payment");
    }

    [Fact]
    public void ValidateTransaction_ShouldHandleMultipleRules()
    {
        // Arrange
        var rules = new List<ComplianceRule>
        {
            new("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true),
            new("SA", "SAMA", "Saudi Arabian Monetary Authority requirements", true, true, true),
            new("IQ", "CBI", "Central Bank of Iraq requirements", false, true, false)
        };
        var engine = new RegulatoryRulesEngine(rules, _logger);

        var payment1 = CreateTestPayment();
        payment1.CompleteThreeDSecure(new Payment.Domain.ValueObjects.ThreeDSecureResult(
            authenticated: true,
            cavv: "test-cavv",
            eci: "05",
            xid: null,
            version: "2.1.0",
            failureReason: null));

        var payment2 = CreateTestPayment();

        // Act
        var result1 = engine.ValidateTransaction("KW", payment1);
        var result2 = engine.ValidateTransaction("SA", payment1);
        var result3 = engine.ValidateTransaction("IQ", payment2);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenRulesIsNull()
    {
        // Act
        var act = () => new RegulatoryRulesEngine(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("rules");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var rules = new List<ComplianceRule>();

        // Act
        var act = () => new RegulatoryRulesEngine(rules, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private static PaymentEntity CreateTestPayment()
    {
        var id = PaymentId.NewId();
        var amount = Amount.FromDecimal(100.00m);
        var currency = Currency.USD;
        var paymentMethod = PaymentMethod.CreditCard;
        var provider = PaymentProvider.ZainCash;
        var merchantId = "merchant-123";
        var orderId = "order-456";

        return new PaymentEntity(id, amount, currency, paymentMethod, provider, merchantId, orderId);
    }
}

