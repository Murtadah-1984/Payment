# GraphQL Integration Tests

Comprehensive integration tests for the Payment Microservice GraphQL API.

## Test Structure

### Test Files

1. **GraphQLTestFixture.cs** - Test fixture providing:
   - `WebApplicationFactory<Program>` for integration testing
   - JWT token generation for authentication
   - In-memory database configuration
   - Authenticated HTTP client creation

2. **GraphQLQueriesTests.cs** - Tests for GraphQL queries:
   - `GetPaymentById` - Get payment by ID
   - `GetPaymentByOrderId` - Get payment by order ID
   - `GetPaymentsByMerchant` - Get payments by merchant
   - Field selection validation
   - Authentication requirements

3. **GraphQLMutationsTests.cs** - Tests for GraphQL mutations:
   - `CreatePayment` - Create new payment (simple and with split rule)
   - `ProcessPayment` - Mark payment as processing
   - `CompletePayment` - Mark payment as completed
   - `FailPayment` - Mark payment as failed
   - `RefundPayment` - Refund a payment
   - Input validation

4. **GraphQLAuthenticationTests.cs** - Authentication and authorization tests:
   - JWT token validation
   - Unauthorized access handling
   - Scope-based authorization (read/write)
   - Invalid token rejection
   - Expired token handling

5. **GraphQLErrorHandlingTests.cs** - Error handling and validation tests:
   - Invalid query handling
   - Malformed query handling
   - Missing required fields
   - Invalid input types
   - Error message formatting
   - Partial failure handling

## Running Tests

```bash
# Run all GraphQL tests
dotnet test --filter "FullyQualifiedName~GraphQL"

# Run specific test class
dotnet test --filter "FullyQualifiedName~GraphQLQueriesTests"

# Run with coverage
dotnet test --filter "FullyQualifiedName~GraphQL" /p:CollectCoverage=true
```

## Test Coverage

The GraphQL tests cover:

- ✅ All GraphQL queries (3 queries)
- ✅ All GraphQL mutations (5 mutations)
- ✅ Authentication and authorization
- ✅ Error handling and validation
- ✅ Input validation
- ✅ Field selection
- ✅ JWT token validation
- ✅ Scope-based authorization

## Test Patterns

### AAA Pattern
All tests follow the Arrange-Act-Assert pattern:

```csharp
[Fact]
public async Task TestName_ShouldDoSomething_WhenCondition()
{
    // Arrange
    var query = @"query { ... }";
    var request = new { query };

    // Act
    var response = await _client.PostAsJsonAsync("/graphql", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    // ... additional assertions
}
```

### Test Fixture
Tests use `IClassFixture<GraphQLTestFixture>` for shared test infrastructure:

```csharp
public class GraphQLQueriesTests : IClassFixture<GraphQLTestFixture>
{
    private readonly GraphQLTestFixture _fixture;
    private readonly HttpClient _client;

    public GraphQLQueriesTests(GraphQLTestFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.CreateAuthenticatedClient();
    }
}
```

## Notes

- Tests use in-memory database for isolation
- JWT tokens are generated with test keys
- Authentication is disabled for some tests to validate authorization
- Tests validate GraphQL response structure and error handling
- Some tests expect null responses when entities don't exist (expected in test environment)

