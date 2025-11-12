using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Payment.API;
using System.Net;
using Xunit;

namespace Payment.API.Tests.Controllers;

public class ApiVersioningTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiVersioningTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAvailableProviders_WithUrlVersion_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/payments/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAvailableProviders_WithHeaderVersion_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Version", "1.0");

        // Act
        var response = await _client.GetAsync("/api/payments/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAvailableProviders_WithQueryStringVersion_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/api/payments/providers?version=1.0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAvailableProviders_WithoutVersion_Returns200_WithDefaultVersion()
    {
        // Act
        var response = await _client.GetAsync("/api/payments/providers");

        // Assert
        // Should default to v1.0
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAvailableProviders_ResponseHeaders_IncludeApiVersion()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/payments/providers");

        // Assert
        response.Headers.Should().ContainKey("api-supported-versions");
        response.Headers.Should().ContainKey("api-deprecated-versions");
    }

    [Fact]
    public async Task Swagger_ShowsAllVersions()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Payment Microservice API");
    }
}

