---
title: GraphQL Support
version: 1.0
last_updated: 2025-01-27
category: Payment
tags:
  - graphql
  - api
  - queries
  - mutations
summary: >
  GraphQL API support for the Payment Microservice, providing flexible client queries
  and mutations following Clean Architecture principles.
related_docs:
  - Payment_Microservice.md
  - ../01-Architecture/System_Architecture.md
ai_context_priority: medium
---

# 🔷 GraphQL Support

The Payment Microservice includes **GraphQL API support** using **HotChocolate** for flexible client queries and mutations. The implementation follows **Clean Architecture** principles, with the GraphQL layer delegating to the Application layer via **MediatR** (CQRS pattern).

## 🎯 Features

- ✅ **GraphQL Queries** - Flexible querying of payment data
- ✅ **GraphQL Mutations** - Payment operations via GraphQL
- ✅ **JWT Authentication** - Secure GraphQL endpoints
- ✅ **Clean Architecture** - GraphQL layer delegates to Application layer
- ✅ **Stateless Design** - Kubernetes-ready, horizontally scalable
- ✅ **Banana Cake Pop** - Interactive GraphQL IDE (development only)

## 📍 Endpoints

- **GraphQL Endpoint**: `/graphql` - Main GraphQL API endpoint
- **GraphQL IDE (Banana Cake Pop)**: `/graphql` - Interactive GraphQL IDE (development only)
- **GraphQL Schema**: `/graphql/schema` - SDL schema export

**Note:** In production, the GraphQL IDE is disabled for security. Use the `/graphql` endpoint for all GraphQL operations.

## 🔐 Authentication

All GraphQL queries and mutations require **JWT Bearer Token** authentication. Include the token in the `Authorization` header:

```
Authorization: Bearer {your-jwt-token}
```

## 📊 GraphQL Schema

### Types

#### PaymentType
```graphql
type PaymentType {
  id: ID!
  amount: Decimal!
  currency: String!
  paymentMethod: String!
  provider: String!
  merchantId: String!
  orderId: String!
  status: String!
  transactionId: String
  failureReason: String
  splitPayment: SplitPaymentType
  metadata: JSON
  cardToken: CardTokenType
  createdAt: DateTime!
  updatedAt: DateTime!
}
```

**Note:** The `metadata` field is a JSON scalar type that can contain arbitrary key-value pairs.

#### SplitPaymentType
```graphql
type SplitPaymentType {
  systemShare: Decimal!
  ownerShare: Decimal!
  systemFeePercent: Decimal!
}
```

#### CardTokenType
```graphql
type CardTokenType {
  last4Digits: String!
  cardBrand: String!
}
```

### Queries

#### GetPaymentById
Get a payment by its ID.

```graphql
query {
  getPaymentById(paymentId: "123e4567-e89b-12d3-a456-426614174000") {
    id
    amount
    currency
    status
    merchantId
    orderId
    createdAt
  }
}
```

#### GetPaymentByOrderId
Get a payment by order ID.

```graphql
query {
  getPaymentByOrderId(orderId: "ORDER-12345") {
    id
    amount
    currency
    status
    transactionId
  }
}
```

#### GetPaymentsByMerchant
Get all payments for a merchant.

```graphql
query {
  getPaymentsByMerchant(merchantId: "MERCHANT-001") {
    id
    amount
    currency
    status
    orderId
    createdAt
  }
}
```

### Mutations

#### CreatePayment
Create a new payment.

**Simple Payment:**
```graphql
mutation {
  createPayment(input: {
    requestId: "123e4567-e89b-12d3-a456-426614174000"
    amount: 100.50
    currency: "USD"
    paymentMethod: "Card"
    provider: "Stripe"
    merchantId: "MERCHANT-001"
    orderId: "ORDER-12345"
    projectCode: "PROJECT-001"
    idempotencyKey: "unique-key-12345"
    systemFeePercent: 2.5
    customerEmail: "customer@example.com"
    metadata: {
      "projectId": "PROJECT-001",
      "userId": "user-123"
    }
  }) {
    id
    amount
    currency
    status
    createdAt
  }
}
```

**Payment with Split Rule:**
```graphql
mutation {
  createPayment(input: {
    requestId: "123e4567-e89b-12d3-a456-426614174000"
    amount: 1000.00
    currency: "USD"
    paymentMethod: "Card"
    provider: "Stripe"
    merchantId: "MERCHANT-001"
    orderId: "ORDER-12345"
    projectCode: "PROJECT-001"
    idempotencyKey: "unique-key-12345"
    systemFeePercent: 5.0
    splitRule: {
      systemFeePercent: 5.0
      accounts: [
        {
          accountType: "SystemOwner"
          accountIdentifier: "IBAN-123456"
          percentage: 5.0
        },
        {
          accountType: "ServiceOwner"
          accountIdentifier: "WALLET-789"
          percentage: 95.0
        }
      ]
    }
    customerEmail: "customer@example.com"
  }) {
    id
    amount
    currency
    status
    splitPayment {
      systemShare
      ownerShare
      systemFeePercent
    }
    createdAt
  }
}
```

#### ProcessPayment
Mark a payment as processing.

```graphql
mutation {
  processPayment(
    paymentId: "123e4567-e89b-12d3-a456-426614174000"
    transactionId: "TXN-12345"
  ) {
    id
    status
    transactionId
  }
}
```

#### CompletePayment
Mark a payment as completed.

```graphql
mutation {
  completePayment(
    paymentId: "123e4567-e89b-12d3-a456-426614174000"
  ) {
    id
    status
    updatedAt
  }
}
```

#### FailPayment
Mark a payment as failed.

```graphql
mutation {
  failPayment(
    paymentId: "123e4567-e89b-12d3-a456-426614174000"
    reason: "Insufficient funds"
  ) {
    id
    status
    failureReason
  }
}
```

#### RefundPayment
Refund a completed payment.

```graphql
mutation {
  refundPayment(
    paymentId: "123e4567-e89b-12d3-a456-426614174000"
    refundTransactionId: "REFUND-12345"
  ) {
    id
    status
    updatedAt
  }
}
```

## 🏗️ Architecture

The GraphQL implementation follows **Clean Architecture** principles:

```
┌──────────────────────────────────────────┐
│ GraphQL Layer → Payment.API/GraphQL      │
│   • Queries (PaymentQueries)              │
│   • Mutations (PaymentMutations)          │
│   • Types (PaymentType, InputTypes)       │
│   • Mappings (PaymentGraphQLMappings)     │
├──────────────────────────────────────────┤
│ Application Layer → Payment.Application   │
│   • Commands & Queries (CQRS/MediatR)      │
│   • Handlers (Use Cases)                  │
│   • DTOs                                   │
└──────────────────────────────────────────┘
```

### Dependency Flow

- **GraphQL Layer** → **Application Layer** (via MediatR)
- **No direct dependencies** on Infrastructure layer
- **Stateless** - no session state, suitable for Kubernetes

## 📝 Implementation Details

### GraphQL Types

Located in `src/Payment.API/GraphQL/Types/`:
- `PaymentType.cs` - Payment, SplitPayment, CardToken types
- `InputTypes.cs` - Input types for mutations

### GraphQL Resolvers

Located in `src/Payment.API/GraphQL/`:
- `Queries/PaymentQueries.cs` - Query resolvers
- `Mutations/PaymentMutations.cs` - Mutation resolvers

### Mappings

Located in `src/Payment.API/GraphQL/Mappings/`:
- `PaymentGraphQLMappings.cs` - DTO ↔ GraphQL type mappings

### Configuration

GraphQL is configured in `Program.cs`:

```csharp
builder.Services
    .AddGraphQLServer()
    .AddQueryType<PaymentQueries>()
    .AddMutationType<PaymentMutations>()
    .AddAuthorization()
    .ModifyRequestOptions(options =>
    {
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    });
```

## 🔒 Security

- **JWT Authentication** - All queries and mutations require authentication
- **Authorization Policies** - Uses ASP.NET Core authorization policies
- **Input Validation** - All inputs are validated via FluentValidation
- **Stateless** - No session state, suitable for microservices
- **Rate Limiting** - GraphQL endpoints are subject to the same rate limiting as REST endpoints
- **CORS** - Configured CORS policies apply to GraphQL endpoints
- **Request Sanitization** - All GraphQL requests go through the same sanitization middleware

### Error Handling

GraphQL errors are returned in a standardized format:

```json
{
  "errors": [
    {
      "message": "Payment not found",
      "extensions": {
        "code": "PAYMENT_NOT_FOUND",
        "statusCode": 404
      }
    }
  ],
  "data": null
}
```

In development mode, additional exception details are included for debugging.

## 🚀 Usage Examples

### Using curl

```bash
curl -X POST https://localhost:5001/graphql \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {your-jwt-token}" \
  -d '{
    "query": "query { getPaymentById(paymentId: \"123e4567-e89b-12d3-a456-426614174000\") { id amount currency status } }"
  }'
```

### Using GraphQL Client (C#)

```csharp
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using System.Net.Http.Headers;

var client = new GraphQLHttpClient(
    "https://localhost:5001/graphql", 
    new NewtonsoftJsonSerializer());

client.HttpClient.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", token);

var request = new GraphQLRequest
{
    Query = @"
        query {
            getPaymentById(paymentId: ""123e4567-e89b-12d3-a456-426614174000"") {
                id
                amount
                currency
                status
                merchantId
                orderId
                createdAt
            }
        }
    "
};

var response = await client.SendQueryAsync<PaymentResponse>(request);
```

### Using JavaScript/TypeScript (Apollo Client)

```typescript
import { ApolloClient, InMemoryCache, gql, createHttpLink } from '@apollo/client';
import { setContext } from '@apollo/client/link/context';

const httpLink = createHttpLink({
  uri: 'https://localhost:5001/graphql',
});

const authLink = setContext((_, { headers }) => {
  const token = localStorage.getItem('jwt_token');
  return {
    headers: {
      ...headers,
      authorization: token ? `Bearer ${token}` : "",
    }
  };
});

const client = new ApolloClient({
  link: authLink.concat(httpLink),
  cache: new InMemoryCache()
});

const GET_PAYMENT = gql`
  query GetPayment($paymentId: ID!) {
    getPaymentById(paymentId: $paymentId) {
      id
      amount
      currency
      status
      merchantId
      orderId
      createdAt
    }
  }
`;

// Usage
const { data } = await client.query({
  query: GET_PAYMENT,
  variables: { paymentId: "123e4567-e89b-12d3-a456-426614174000" }
});
```

### Using Python (gql)

```python
from gql import Client, gql
from gql.transport.aiohttp import AIOHTTPTransport

transport = AIOHTTPTransport(
    url="https://localhost:5001/graphql",
    headers={"Authorization": f"Bearer {token}"}
)

client = Client(transport=transport, fetch_schema_from_transport=True)

query = gql("""
    query {
        getPaymentById(paymentId: "123e4567-e89b-12d3-a456-426614174000") {
            id
            amount
            currency
            status
        }
    }
""")

result = await client.execute_async(query)
```

## 📚 Related Documentation

- [Payment Microservice API](Payment_Microservice.md) - REST API documentation
- [System Architecture](../01-Architecture/System_Architecture.md) - Architecture overview
- [Security Policy](Security_Policy.md) - Security features and compliance

## ✅ SOLID Principles

The GraphQL implementation follows **SOLID principles**:

- **S** - Single Responsibility: Each resolver handles one operation
- **O** - Open/Closed: Extensible via HotChocolate's type system
- **L** - Liskov Substitution: GraphQL types can be substituted
- **I** - Interface Segregation: Queries and mutations are separated
- **D** - Dependency Inversion: Depends on MediatR abstractions, not implementations

## 🎯 Benefits

1. **Flexible Queries** - Clients request only the data they need, reducing payload size
2. **Single Endpoint** - One endpoint for all operations, simplifying client integration
3. **Type Safety** - Strongly typed schema with automatic validation
4. **Developer Experience** - Interactive GraphQL IDE (Banana Cake Pop) for exploration
5. **Performance** - Reduced over-fetching and under-fetching of data
6. **Clean Architecture** - Maintains separation of concerns, delegates to Application layer
7. **Versioning** - Schema evolution without breaking changes
8. **Real-time Capabilities** - Can be extended with subscriptions for real-time updates

## 🔄 GraphQL vs REST API

| Feature | GraphQL | REST API |
|---------|---------|----------|
| **Endpoint** | Single `/graphql` endpoint | Multiple endpoints per resource |
| **Data Fetching** | Client specifies exact fields | Server returns fixed structure |
| **Over-fetching** | Avoided - only requested fields | May return unnecessary data |
| **Under-fetching** | Avoided - single request for related data | May require multiple requests |
| **Caching** | More complex, requires client-side caching | HTTP caching works out of the box |
| **Learning Curve** | Requires GraphQL knowledge | Standard HTTP/REST knowledge |
| **Tooling** | GraphQL IDE, schema introspection | Swagger/OpenAPI documentation |

**Recommendation:** Use GraphQL when you need flexible queries and want to reduce payload size. Use REST API for simpler integrations or when HTTP caching is critical.

## 📊 Performance Considerations

- **Query Complexity** - HotChocolate automatically analyzes query complexity
- **Pagination** - Consider implementing pagination for large result sets
- **Caching** - Use client-side caching for frequently accessed data
- **Batch Operations** - GraphQL supports batching multiple operations in a single request

## 🧪 Testing GraphQL

### Using Banana Cake Pop (Development)

1. Navigate to `https://localhost:5001/graphql` in your browser
2. Authenticate using the JWT token in the HTTP Headers section
3. Write and execute queries/mutations interactively
4. Explore the schema documentation

### Integration Testing

```csharp
[Fact]
public async Task GetPaymentById_ReturnsPayment()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", _token);

    var query = @"
        query {
            getPaymentById(paymentId: ""{paymentId}"") {
                id
                amount
                currency
                status
            }
        }
    ".Replace("{paymentId}", _paymentId.ToString());

    var request = new { query };

    // Act
    var response = await client.PostAsJsonAsync("/graphql", request);

    // Assert
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    // Assert content...
}
```

