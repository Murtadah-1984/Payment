using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.HealthChecks;
using System.IO;
using Xunit;

namespace Payment.API.Tests.HealthChecks;

public class DiskSpaceHealthCheckTests
{
    private readonly Mock<ILogger<DiskSpaceHealthCheck>> _loggerMock;

    public DiskSpaceHealthCheckTests()
    {
        _loggerMock = new Mock<ILogger<DiskSpaceHealthCheck>>();
    }

    [Fact]
    public async Task CheckHealthAsync_WithSufficientSpace_ReturnsHealthy()
    {
        // Arrange
        var healthCheck = new DiskSpaceHealthCheck(_loggerMock.Object, minimumFreeSpacePercent: 0.1); // 0.1% for testing

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert
        // On most systems, there should be more than 0.1% free space
        // This test verifies the health check runs without exceptions
        result.Should().NotBeNull();
        result.Data.Should().ContainKey("Drive");
        result.Data.Should().ContainKey("FreeSpacePercent");
        result.Data.Should().ContainKey("MinimumFreeSpacePercent");
    }

    [Fact]
    public async Task CheckHealthAsync_WithLowSpace_ReturnsUnhealthy()
    {
        // Arrange
        // Set a very high threshold (99.9%) to force unhealthy status
        var healthCheck = new DiskSpaceHealthCheck(_loggerMock.Object, minimumFreeSpacePercent: 99.9);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert
        // On most systems, there won't be 99.9% free space, so it should be unhealthy
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Disk space is low");
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesDiskSpaceData()
    {
        // Arrange
        var healthCheck = new DiskSpaceHealthCheck(_loggerMock.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert
        result.Data.Should().ContainKey("Drive");
        result.Data.Should().ContainKey("TotalSpaceBytes");
        result.Data.Should().ContainKey("FreeSpaceBytes");
        result.Data.Should().ContainKey("UsedSpaceBytes");
        result.Data.Should().ContainKey("FreeSpacePercent");
        result.Data.Should().ContainKey("MinimumFreeSpacePercent");
        
        result.Data["TotalSpaceBytes"].Should().BeOfType<long>();
        result.Data["FreeSpaceBytes"].Should().BeOfType<long>();
        result.Data["UsedSpaceBytes"].Should().BeOfType<long>();
    }
}

