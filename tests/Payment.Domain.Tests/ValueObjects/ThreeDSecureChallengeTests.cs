using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

/// <summary>
/// Unit tests for ThreeDSecureChallenge value object.
/// Tests cover validation, immutability, and value object behavior.
/// </summary>
public class ThreeDSecureChallengeTests
{
    [Fact]
    public void Constructor_ShouldCreateChallenge_WhenValidParameters()
    {
        // Arrange
        var acsUrl = "https://acs.example.com/authenticate";
        var pareq = "base64-encoded-pareq";
        var md = "merchant-data-123";
        var termUrl = "https://example.com/return";
        var version = "2.2.0";

        // Act
        var challenge = new ThreeDSecureChallenge(acsUrl, pareq, md, termUrl, version);

        // Assert
        challenge.AcsUrl.Should().Be(acsUrl);
        challenge.Pareq.Should().Be(pareq);
        challenge.Md.Should().Be(md);
        challenge.TermUrl.Should().Be(termUrl);
        challenge.Version.Should().Be(version);
    }

    [Fact]
    public void Constructor_ShouldUseDefaultVersion_WhenVersionNotProvided()
    {
        // Arrange
        var acsUrl = "https://acs.example.com/authenticate";
        var pareq = "base64-encoded-pareq";
        var md = "merchant-data-123";
        var termUrl = "https://example.com/return";

        // Act
        var challenge = new ThreeDSecureChallenge(acsUrl, pareq, md, termUrl);

        // Assert
        challenge.Version.Should().Be("2.2.0");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenAcsUrlIsNullOrEmpty(string? acsUrl)
    {
        // Act & Assert
        var act = () => new ThreeDSecureChallenge(
            acsUrl!,
            "pareq",
            "md",
            "termUrl");

        act.Should().Throw<ArgumentException>()
            .WithMessage("ACS URL cannot be null or empty*")
            .And.ParamName.Should().Be("acsUrl");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenPareqIsNullOrEmpty(string? pareq)
    {
        // Act & Assert
        var act = () => new ThreeDSecureChallenge(
            "https://acs.example.com",
            pareq!,
            "md",
            "termUrl");

        act.Should().Throw<ArgumentException>()
            .WithMessage("PAReq cannot be null or empty*")
            .And.ParamName.Should().Be("pareq");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenMdIsNullOrEmpty(string? md)
    {
        // Act & Assert
        var act = () => new ThreeDSecureChallenge(
            "https://acs.example.com",
            "pareq",
            md!,
            "termUrl");

        act.Should().Throw<ArgumentException>()
            .WithMessage("Merchant data cannot be null or empty*")
            .And.ParamName.Should().Be("md");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenTermUrlIsNullOrEmpty(string? termUrl)
    {
        // Act & Assert
        var act = () => new ThreeDSecureChallenge(
            "https://acs.example.com",
            "pareq",
            "md",
            termUrl!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Term URL cannot be null or empty*")
            .And.ParamName.Should().Be("termUrl");
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenSameValues()
    {
        // Arrange
        var challenge1 = new ThreeDSecureChallenge(
            "https://acs.example.com",
            "pareq",
            "md",
            "termUrl",
            "2.2.0");

        var challenge2 = new ThreeDSecureChallenge(
            "https://acs.example.com",
            "pareq",
            "md",
            "termUrl",
            "2.2.0");

        // Act & Assert
        challenge1.Should().Be(challenge2);
        challenge1.Equals(challenge2).Should().BeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenDifferentValues()
    {
        // Arrange
        var challenge1 = new ThreeDSecureChallenge(
            "https://acs.example.com",
            "pareq1",
            "md",
            "termUrl",
            "2.2.0");

        var challenge2 = new ThreeDSecureChallenge(
            "https://acs.example.com",
            "pareq2",
            "md",
            "termUrl",
            "2.2.0");

        // Act & Assert
        challenge1.Should().NotBe(challenge2);
        challenge1.Equals(challenge2).Should().BeFalse();
    }
}

