using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.HealthChecks;
using Payment.Application.Services;
using Xunit;

namespace Payment.API.Tests.HealthChecks;

public class PaymentProviderHealthCheckTests
{
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly Mock<ILogger<PaymentProviderHealthCheck>> _loggerMock;
    private readonly PaymentProviderHealthCheck _healthCheck;

    public PaymentProviderHealthCheckTests()
    {
        _providerFactoryMock = new Mock<IPaymentProviderFactory>();
        _loggerMock = new Mock<ILogger<PaymentProviderHealthCheck>>();
        _healthCheck = new PaymentProviderHealthCheck(_providerFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WithProviders_ReturnsHealthy()
    {
        // Arrange
        var providers = new[] { "ZainCash", "Stripe", "FIB" };
        _providerFactoryMock
            .Setup(f => f.GetAvailableProviders())
            .Returns(providers);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("ProviderCount");
        result.Data["ProviderCount"].Should().Be(3);
        result.Data.Should().ContainKey("Providers");
    }

    [Fact]
    public async Task CheckHealthAsync_WithNoProviders_ReturnsUnhealthy()
    {
        // Arrange
        _providerFactoryMock
            .Setup(f => f.GetAvailableProviders())
            .Returns(Enumerable.Empty<string>());

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("No payment providers registered");
    }

    [Fact]
    public async Task CheckHealthAsync_WithException_ReturnsUnhealthy()
    {
        // Arrange
        _providerFactoryMock
            .Setup(f => f.GetAvailableProviders())
            .Throws(new Exception("Provider factory error"));

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().NotBeNull();
    }
}

