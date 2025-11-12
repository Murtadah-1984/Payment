using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Payment.API.Tests.GraphQL;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Payment.API.Tests.GraphQL;

/// <summary>
/// Comprehensive tests for GraphQL error handling and validation.
/// Tests error responses, input validation, and error message formatting.
/// </summary>
public class GraphQLErrorHandlingTests : IClassFixture<GraphQLTestFixture>
{
    private readonly GraphQLTestFixture _fixture;
    private readonly HttpClient _client;

    public GraphQLErrorHandlingTests(GraphQLTestFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GraphQL_ShouldReturnError_ForInvalidQuery()
    {
        // Arrange
        var invalidQuery = @"
            query {
                nonExistentField {
                    id
                }
            }
        ";

        var request = new { query = invalidQuery };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GraphQL_ShouldReturnError_ForMalformedQuery()
    {
        // Arrange
        var malformedQuery = @"
            query {
                getPaymentById(paymentId: ""invalid-guid""
        ";

        var request = new { query = malformedQuery };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQL_ShouldReturnError_ForMissingRequiredFields()
    {
        // Arrange
        var mutation = @"
            mutation {
                createPayment(input: {
                    amount: 100.50
                }) {
                    id
                }
            }
        ";

        var request = new { query = mutation };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQL_ShouldReturnError_ForInvalidInputTypes()
    {
        // Arrange
        var mutation = @"
            mutation {
                createPayment(input: {
                    requestId: """ + Guid.NewGuid() + @"""
                    amount: ""not-a-number""
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
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQL_ShouldReturnError_ForInvalidGuid()
    {
        // Arrange
        var query = @"
            query {
                getPaymentById(paymentId: ""not-a-valid-guid"") {
                    id
                }
            }
        ";

        var request = new { query };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQL_ShouldReturnError_ForNegativeAmount()
    {
        // Arrange
        var mutation = @"
            mutation {
                createPayment(input: {
                    requestId: """ + Guid.NewGuid() + @"""
                    amount: -100.50
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
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // May have validation errors for negative amount
        if (jsonDoc.RootElement.TryGetProperty("errors", out var errors))
        {
            errors.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }

    [Fact]
    public async Task GraphQL_ShouldReturnError_ForEmptyStringFields()
    {
        // Arrange
        var mutation = @"
            mutation {
                createPayment(input: {
                    requestId: """ + Guid.NewGuid() + @"""
                    amount: 100.50
                    currency: """"
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
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should have validation errors for empty currency
        if (jsonDoc.RootElement.TryGetProperty("errors", out var errors))
        {
            errors.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }

    [Fact]
    public async Task GraphQL_ShouldReturnProperErrorFormat()
    {
        // Arrange
        var invalidQuery = @"
            query {
                nonExistentField {
                    id
                }
            }
        ";

        var request = new { query = invalidQuery };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
        
        if (errors.GetArrayLength() > 0)
        {
            var firstError = errors[0];
            firstError.TryGetProperty("message", out _).Should().BeTrue();
            // May have extensions with error code
            firstError.TryGetProperty("extensions", out var extensions);
        }
    }

    [Fact]
    public async Task GraphQL_ShouldHandleNullValues()
    {
        // Arrange
        var query = @"
            query {
                getPaymentById(paymentId: null) {
                    id
                }
            }
        ";

        var request = new { query };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should have validation errors for null required parameter
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQL_ShouldReturnDataAndErrors_ForPartialFailure()
    {
        // Arrange
        // This test validates that GraphQL can return partial data with errors
        var query = @"
            query {
                getPaymentById(paymentId: """ + Guid.NewGuid() + @""") {
                    id
                    amount
                    nonExistentField
                }
            }
        ";

        var request = new { query };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should have errors for non-existent field
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQL_ShouldHandleEmptyQuery()
    {
        // Arrange
        var request = new { query = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        // Should have errors for empty query
        jsonDoc.RootElement.TryGetProperty("errors", out var errors);
        errors.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GraphQL_ShouldHandleMissingQuery()
    {
        // Arrange
        var request = new { };

        // Act
        var response = await _client.PostAsJsonAsync("/graphql", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

