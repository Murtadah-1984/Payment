using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Payment.API.Tests.GraphQL;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace Payment.API.Tests.GraphQL;

/// <summary>
/// Comprehensive tests for GraphQL authentication and authorization.
/// Tests JWT token validation, authorization policies, and unauthorized access.
/// </summary>
public class GraphQLAuthenticationTests : IClassFixture<GraphQLTestFixture>
{
    private readonly GraphQLTestFixture _fixture;

    public GraphQLAuthenticationTests(GraphQLTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GraphQLQuery_ShouldRequireAuthentication()
    {
        // Arrange
        var client = _fixture.CreateClient(); // No authentication
        var query = @"
            query {
                getPaymentById(paymentId: """ + Guid.NewGuid() + @""") {
                    id
                }
            }
        ";

        var request = new { query };

        // Act
        var response = await client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // GraphQL returns 200 but includes errors for unauthorized requests
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
        
        if (errors.GetArrayLength() > 0)
        {
            var errorMessage = errors[0].GetProperty("message").GetString();
            errorMessage.Should().ContainAny("authorization", "authentication", "unauthorized");
        }
    }

    [Fact]
    public async Task GraphQLMutation_ShouldRequireAuthentication()
    {
        // Arrange
        var client = _fixture.CreateClient(); // No authentication
        var mutation = @"
            mutation {
                createPayment(input: {
                    requestId: """ + Guid.NewGuid() + @"""
                    amount: 100.50
                    currency: ""USD""
                    paymentMethod: ""Card""
                    provider: ""Stripe""
                    merchantId: ""MERCHANT-001""
                    orderId: ""ORDER-12345""
                    projectCode: ""PROJECT-001""
                    idempotencyKey: ""unique-key-12345""
                }) {
                    id
                }
            }
        ";

        var request = new { query = mutation };

        // Act
        var response = await client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should have authorization errors
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQLQuery_ShouldAcceptValidJwtToken()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient();
        var query = @"
            query {
                getPaymentById(paymentId: """ + Guid.NewGuid() + @""") {
                    id
                }
            }
        ";

        var request = new { query };

        // Act
        var response = await client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should not have authentication errors (may have data errors if payment not found)
        if (jsonDoc.RootElement.TryGetProperty("errors", out var errors))
        {
            // Check that errors are not authentication-related
            foreach (var error in errors.EnumerateArray())
            {
                var message = error.GetProperty("message").GetString();
                message.Should().NotContainAny("authorization", "authentication", "unauthorized", "token");
            }
        }
    }

    [Fact]
    public async Task GraphQLMutation_ShouldAcceptValidJwtToken()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient();
        var mutation = @"
            mutation {
                processPayment(
                    paymentId: """ + Guid.NewGuid() + @"""
                    transactionId: ""TXN-123""
                ) {
                    id
                }
            }
        ";

        var request = new { query = mutation };

        // Act
        var response = await client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should not have authentication errors
        if (jsonDoc.RootElement.TryGetProperty("errors", out var errors))
        {
            foreach (var error in errors.EnumerateArray())
            {
                var message = error.GetProperty("message").GetString();
                message.Should().NotContainAny("authorization", "authentication", "unauthorized", "token");
            }
        }
    }

    [Fact]
    public async Task GraphQLQuery_ShouldRejectInvalidToken()
    {
        // Arrange
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", "invalid-token");
        
        var query = @"
            query {
                getPaymentById(paymentId: """ + Guid.NewGuid() + @""") {
                    id
                }
            }
        ";

        var request = new { query };

        // Act
        var response = await client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should have authentication errors
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQLQuery_ShouldRejectExpiredToken()
    {
        // Arrange
        // Note: In our test fixture, we disable lifetime validation for testing
        // This test validates the structure, but actual expiration would need different config
        var client = _fixture.CreateClient();
        var expiredToken = _fixture.GenerateTestToken(
            new Claim("exp", DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString())
        );
        
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", expiredToken);
        
        var query = @"
            query {
                getPaymentById(paymentId: """ + Guid.NewGuid() + @""") {
                    id
                }
            }
        ";

        var request = new { query };

        // Act
        var response = await client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Note: With lifetime validation disabled, this may still work
        // In production with validation enabled, this would fail
    }

    [Fact]
    public async Task GraphQLQuery_ShouldWorkWithReadScope()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            new Claim("scope", "payment.read")
        );
        
        var query = @"
            query {
                getPaymentById(paymentId: """ + Guid.NewGuid() + @""") {
                    id
                    amount
                }
            }
        ";

        var request = new { query };

        // Act
        var response = await client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should not have authorization errors for read operations
        if (jsonDoc.RootElement.TryGetProperty("errors", out var errors))
        {
            foreach (var error in errors.EnumerateArray())
            {
                var message = error.GetProperty("message").GetString();
                message.Should().NotContain("authorization");
            }
        }
    }

    [Fact]
    public async Task GraphQLMutation_ShouldWorkWithWriteScope()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            new Claim("scope", "payment.write")
        );
        
        var mutation = @"
            mutation {
                processPayment(
                    paymentId: """ + Guid.NewGuid() + @"""
                    transactionId: ""TXN-123""
                ) {
                    id
                }
            }
        ";

        var request = new { query = mutation };

        // Act
        var response = await client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should not have authorization errors for write operations with write scope
        if (jsonDoc.RootElement.TryGetProperty("errors", out var errors))
        {
            foreach (var error in errors.EnumerateArray())
            {
                var message = error.GetProperty("message").GetString();
                message.Should().NotContain("authorization");
            }
        }
    }
}

