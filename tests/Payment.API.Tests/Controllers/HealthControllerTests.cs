using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Payment.API.Controllers;
using Xunit;

namespace Payment.API.Tests.Controllers;

public class HealthControllerTests
{
    [Fact]
    public void Get_ShouldReturnOk_WithHealthStatus()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.Get();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void Get_ShouldReturnHealthyStatus()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.Get() as OkObjectResult;
        var value = result!.Value;

        // Assert
        value.Should().NotBeNull();
        var statusProperty = value!.GetType().GetProperty("status");
        statusProperty.Should().NotBeNull();
        var statusValue = statusProperty!.GetValue(value);
        statusValue!.ToString().Should().Be("healthy");
    }
}

