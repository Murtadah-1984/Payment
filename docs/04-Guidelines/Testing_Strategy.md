---
title: Testing Strategy
version: 1.0
last_updated: 2025-11-11
category: Guidelines
tags:
  - testing
  - unit tests
  - integration tests
  - test coverage
summary: >
  Comprehensive testing strategy including unit tests, integration tests,
  test coverage requirements, and testing best practices.
related_docs:
  - Coding_Standards.md
  - Naming_Conventions.md
ai_context_priority: high
---

# 🧪 Testing Strategy

## Test Structure

The Payment Microservice includes comprehensive test coverage:

```
tests/
├── Payment.Domain.Tests/          # Domain layer unit tests
├── Payment.Application.Tests/     # Application layer unit tests
├── Payment.Infrastructure.Tests/  # Infrastructure layer unit tests
└── Payment.API.Tests/             # API integration tests
```

## Test Types

### Unit Tests
- Test individual classes and methods in isolation
- Mock dependencies using Moq or NSubstitute
- Fast execution (< 1 second per test)
- High coverage (> 80% for business logic)

### Integration Tests
- Test component interactions
- Use in-memory database for EF Core tests
- Test API endpoints with TestServer
- Verify end-to-end flows

### Test Coverage Requirements

- **Domain Layer**: > 90% coverage
- **Application Layer**: > 85% coverage
- **Infrastructure Layer**: > 80% coverage
- **API Layer**: > 75% coverage

## Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test project
dotnet test tests/Payment.Application.Tests/

# Run specific test class
dotnet test --filter "FullyQualifiedName~PaymentOrchestratorTests"
```

## Testing Best Practices

### AAA Pattern
- **Arrange**: Set up test data and mocks
- **Act**: Execute the code under test
- **Assert**: Verify expected outcomes

### Test Naming
- Use descriptive test names: `MethodName_Scenario_ExpectedBehavior`
- Example: `ProcessPayment_WhenProviderFails_ShouldMarkPaymentAsFailed`

### Mocking Guidelines
- Mock external dependencies (databases, APIs, services)
- Use real objects for value objects and DTOs
- Verify mock interactions when necessary

### Test Data Builders
- Use builder pattern for complex test data
- Create reusable test fixtures
- Use Faker library for random data

## See Also

- [Coding Standards](Coding_Standards.md)
- [Naming Conventions](Naming_Conventions.md)

