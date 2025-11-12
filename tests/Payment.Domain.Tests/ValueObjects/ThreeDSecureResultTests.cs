using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

/// <summary>
/// Unit tests for ThreeDSecureResult value object.
/// Tests cover successful authentication, failure scenarios, and value object behavior.
/// </summary>
public class ThreeDSecureResultTests
{
    [Fact]
    public void Constructor_ShouldCreateResult_WhenAuthenticated()
    {
        // Arrange
        var cavv = "cavv-1234567890123456789012345678";
        var eci = "05";
        var xid = "xid-12345678901234567890";
        var version = "2.2.0";

        // Act
        var result = new ThreeDSecureResult(
            authenticated: true,
            cavv: cavv,
            eci: eci,
            xid: xid,
            version: version);

        // Assert
        result.Authenticated.Should().BeTrue();
        result.Cavv.Should().Be(cavv);
        result.Eci.Should().Be(eci);
        result.Xid.Should().Be(xid);
        result.Version.Should().Be(version);
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldCreateResult_WhenNotAuthenticated()
    {
        // Arrange
        var failureReason = "Authentication failed";

        // Act
        var result = new ThreeDSecureResult(
            authenticated: false,
            failureReason: failureReason);

        // Assert
        result.Authenticated.Should().BeFalse();
        result.FailureReason.Should().Be(failureReason);
        result.Cavv.Should().BeNull();
        result.Eci.Should().BeNull();
        result.Xid.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldCreateResult_WithOptionalParameters()
    {
        // Act
        var result = new ThreeDSecureResult(
            authenticated: true,
            cavv: "cavv-123",
            eci: "05",
            xid: "xid-123",
            version: "2.2.0",
            ares: "ares-response",
            failureReason: null);

        // Assert
        result.Authenticated.Should().BeTrue();
        result.Cavv.Should().Be("cavv-123");
        result.Eci.Should().Be("05");
        result.Xid.Should().Be("xid-123");
        result.Version.Should().Be("2.2.0");
        result.Ares.Should().Be("ares-response");
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenSameValues()
    {
        // Arrange
        var result1 = new ThreeDSecureResult(
            authenticated: true,
            cavv: "cavv-123",
            eci: "05",
            xid: "xid-123",
            version: "2.2.0");

        var result2 = new ThreeDSecureResult(
            authenticated: true,
            cavv: "cavv-123",
            eci: "05",
            xid: "xid-123",
            version: "2.2.0");

        // Act & Assert
        result1.Should().Be(result2);
        result1.Equals(result2).Should().BeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenDifferentAuthenticatedStatus()
    {
        // Arrange
        var result1 = new ThreeDSecureResult(authenticated: true);
        var result2 = new ThreeDSecureResult(authenticated: false);

        // Act & Assert
        result1.Should().NotBe(result2);
        result1.Equals(result2).Should().BeFalse();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenDifferentCavv()
    {
        // Arrange
        var result1 = new ThreeDSecureResult(
            authenticated: true,
            cavv: "cavv-123");

        var result2 = new ThreeDSecureResult(
            authenticated: true,
            cavv: "cavv-456");

        // Act & Assert
        result1.Should().NotBe(result2);
    }
}

