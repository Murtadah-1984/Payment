# 💳 Payment Microservice (Factory + Strategy Ready)

A **production-ready, extensible Payment Microservice** built with **.NET 8**, following **Clean Architecture**, **SOLID principles**, and **stateless microservice** design for **Kubernetes deployment**.

It implements a **Factory + Strategy pattern** with **CQRS (MediatR)** to support multiple payment providers and future extensions.

## 🚀 Quick Start

```bash
# Run with Docker Compose
docker-compose up -d

# Or run locally
dotnet run --project src/Payment.API

# Access Swagger UI
https://localhost:5001/swagger
```

## 📚 Documentation

**All documentation has been moved to the [`docs/`](docs/) directory for better organization and AI context retrieval.**

### 📖 Documentation Structure

- **[Overview](docs/00-Overview/README.md)** - Project overview, features, and prerequisites
- **[Architecture](docs/01-Architecture/)** - System architecture, authentication flow
- **[Payment](docs/02-Payment/)** - API documentation, reporting, security, integrations
- **[Infrastructure](docs/03-Infrastructure/)** - Kubernetes deployment, observability, performance
- **[Guidelines](docs/04-Guidelines/)** - Coding standards, testing, extension guide, contributing

### 🔍 Quick Links

- [System Architecture](docs/01-Architecture/System_Architecture.md) - Clean Architecture and layer documentation
- [API Documentation](docs/02-Payment/Payment_Microservice.md) - Complete REST API reference
- [GraphQL Support](docs/02-Payment/GraphQL_Support.md) - GraphQL API documentation
- [Security Policy](docs/02-Payment/Security_Policy.md) - Security features and compliance
- [Kubernetes Deployment](docs/03-Infrastructure/Kubernetes_Deployment.md) - Deployment guide
- [Extension Guide](docs/04-Guidelines/Extension_Guide.md) - Add new payment providers

### 📑 Documentation Index

For AI context routing and semantic search, see [docs/05-Index.yaml](docs/05-Index.yaml).

## 🎯 Key Features

- ✅ **Clean Architecture** + **SOLID Principles** (SRP, OCP, LSP, ISP, DIP)
- ✅ **CQRS Pattern** with **MediatR** for separation of concerns
- ✅ **Stateless Design** (Kubernetes-Ready, horizontally scalable)
- ✅ **Factory + Strategy Pattern** for extensible payment providers
- ✅ **13 Payment Providers** - ZainCash, AsiaHawala, Stripe, FIB, Square, Helcim, AmazonPaymentServices, Telr, Checkout, Verifone, Paytabs, Tap, TapToPay (NFC/HCE)
- ✅ **Multi-Account Split Payment** (simple or complex distribution rules)
- ✅ **JWT Bearer Token** authentication with external Identity Microservice (OIDC/OAuth2)
- ✅ **Webhook Signature Validation** - HMAC-SHA256 signature validation for all payment callbacks
- ✅ **Webhook Retry Mechanism** - Exponential backoff retry for reliable webhook delivery to external systems
- ✅ **PCI DSS Compliance** - Card tokenization and AES-256 metadata encryption at rest
- ✅ **Secrets Management** - Azure Key Vault, AWS Secrets Manager, Kubernetes Secrets support
- ✅ **Idempotency Keys** - Request hash validation to prevent duplicate payments
- ✅ **Input Validation & Sanitization** - XSS protection, security headers, rate limiting
- ✅ **Audit Logging** - Comprehensive audit trail for compliance
- ✅ **Entity Framework Core** with **PostgreSQL** persistence
- ✅ **Health checks** (`/health`, `/ready`) for K8s probes
- ✅ **OpenAPI/Swagger** documentation with JWT support
- ✅ **Structured logging** (Serilog) with file and console output
- ✅ **OpenTelemetry** integration with Jaeger/Zipkin for distributed tracing
- ✅ **Resilience Patterns** - Circuit Breaker, Retry, Timeout (Polly)
- ✅ **Caching Strategy** - Redis with memory cache fallback
- ✅ **Database Optimization** - Indexes, pagination, query optimization
- ✅ **Event Sourcing & Outbox Pattern** - Reliable event publishing
- ✅ **API Versioning** - URL-based versioning support
- ✅ **State Machine** - Payment status transitions (Stateless library)
- ✅ **Result Pattern** - Functional error handling
- ✅ **Feature Flags** - Microsoft.FeatureManagement
- ✅ **Automated Monthly Reporting** - Financial reports with Prometheus metrics
- ✅ **GraphQL Support** - Flexible client queries and mutations via HotChocolate
- ✅ **Incident Response Service** - Automated payment failure assessment, stakeholder notifications, and automatic refund processing
- ✅ **Country-Based Payment Provider Discovery** - Query available payment providers by country code (IQ, KW, AE, etc.)

## 🏗️ Architecture Overview

```
┌──────────────────────────────────────────┐
│ Presentation Layer → Payment.API          │
│   • Controllers (PaymentsController)      │
│   • GraphQL (Queries, Mutations)          │
│   • JWT Authentication, Swagger, Health   │
│   • Middleware, CORS, Routing             │
├──────────────────────────────────────────┤
│ Application Layer → Payment.Application   │
│   • Commands & Queries (CQRS/MediatR)      │
│   • Handlers (Use Cases)                  │
│   • DTOs, Validators (FluentValidation)   │
│   • Services (Orchestrator, Factory, Split, IncidentResponse)│
│   • Mappings (Entity ↔ DTO)               │
├──────────────────────────────────────────┤
│ Domain Layer → Payment.Domain             │
│   • Entities (Payment)                    │
│   • Value Objects (Amount, Currency, etc.) │
│   • Domain Events (PaymentCompleted, etc.)│
│   • Interfaces (IPaymentProvider, etc.)   │
│   • Enums (PaymentStatus)                 │
├──────────────────────────────────────────┤
│ Infrastructure Layer → Payment.Infrastructure │
│   • EF Core Persistence (PostgreSQL)      │
│   • Repositories (PaymentRepository)      │
│   • Unit of Work Pattern                  │
│   • Payment Providers (12 implementations)│
│   • Circuit Breaker & Notification Services│
│   • Logging (Serilog)                     │
└──────────────────────────────────────────┘
```

### Dependency Flow

- **Presentation** → **Application** → **Domain** ← **Infrastructure**
- Inner layers have **no dependencies** on outer layers
- **Domain** is the **core** with no external dependencies
- **Infrastructure** implements **Domain interfaces**

For detailed architecture documentation, see [System Architecture](docs/01-Architecture/System_Architecture.md).

## 🌍 Country-Based Payment Provider Discovery

The Payment Microservice supports **country-based payment provider discovery**, allowing clients to query available payment providers for specific countries. This feature enables regional payment provider selection based on ISO 3166-1 alpha-2 country codes.

### Usage Examples

#### Get Providers for Iraq (IQ)
```bash
curl -X GET "https://localhost:5001/api/v1/payments/providers/IQ" \
  -H "accept: application/json"
```

**Response:**
```json
[
  {
    "providerName": "ZainCash",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Wallet",
    "isActive": true
  },
  {
    "providerName": "FIB",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Card",
    "isActive": true
  },
  {
    "providerName": "Telr",
    "countryCode": "IQ",
    "currency": "IQD",
    "paymentMethod": "Card",
    "isActive": true
  }
]
```

#### Get Providers for Kuwait (KW)
```bash
curl -X GET "https://localhost:5001/api/v1/payments/providers/KW" \
  -H "accept: application/json"
```

#### Get Providers for UAE (AE)
```bash
curl -X GET "https://localhost:5001/api/v1/payments/providers/AE" \
  -H "accept: application/json"
```

### Regional Extension Guidelines

To add support for new countries or extend existing country support:

#### 1. Update Static Catalog (Default)

Edit `src/Payment.Domain/ValueObjects/PaymentProviderCatalog.cs` and add entries to the `DefaultCatalog`:

```csharp
["US"] = new List<PaymentProviderInfo>
{
    new("Stripe", "US", "USD", "Card", true),
    new("Square", "US", "USD", "Card", true)
}
```

#### 2. Configure via appsettings.json (Recommended)

Add entries to the `PaymentProviderCatalog` section in `appsettings.json`:

```json
{
  "PaymentProviderCatalog": {
    "Providers": [
      {
        "ProviderName": "Stripe",
        "CountryCode": "US",
        "Currency": "USD",
        "PaymentMethod": "Card",
        "IsActive": true
      },
      {
        "ProviderName": "Square",
        "CountryCode": "US",
        "Currency": "USD",
        "PaymentMethod": "Card",
        "IsActive": true
      }
    ]
  }
}
```

#### 3. External Provider Registry (Future)

The catalog supports initialization from external sources. Implement a service that loads providers from:
- Database
- External API
- Configuration service
- Feature flags

Example:
```csharp
var providers = await _externalRegistryService.GetProvidersAsync();
PaymentProviderCatalog.Initialize(providers);
```

### Supported Countries

Currently supported countries with payment providers:

- **IQ (Iraq)**: ZainCash, FIB, Telr, Paytabs, Tap
- **KW (Kuwait)**: Telr, Paytabs, Tap, AmazonPaymentServices, Checkout, Stripe
- **AE (UAE)**: Telr, Paytabs, Tap, AmazonPaymentServices, Checkout, Stripe, Verifone
- **SA (Saudi Arabia)**: Paytabs, Tap, AmazonPaymentServices, Checkout, Stripe
- **BH (Bahrain)**: Telr, Paytabs, Tap, AmazonPaymentServices, Stripe
- **OM (Oman)**: Telr, Paytabs, Tap, AmazonPaymentServices, Stripe
- **QA (Qatar)**: Telr, Paytabs, Tap, AmazonPaymentServices, Checkout, Stripe

### API Endpoint

**GET** `/api/v1/payments/providers/{countryCode}`

- **Authentication**: Not required (AllowAnonymous)
- **Parameters**:
  - `countryCode` (path, required): ISO 3166-1 alpha-2 country code (2 characters)
- **Response**: Array of `PaymentProviderInfoDto` objects
- **Status Codes**:
  - `200 OK`: Successfully retrieved providers (may be empty array)
  - `400 Bad Request`: Invalid country code format

### Architecture

The feature follows **Clean Architecture** principles:

- **Domain Layer**: `PaymentProviderInfo` (value object) and `PaymentProviderCatalog` (static catalog)
- **Application Layer**: `GetPaymentProvidersByCountryQuery` (CQRS query) and handler
- **Presentation Layer**: `PaymentsController` endpoint

Configuration is loaded from `appsettings.json` or can be initialized programmatically, supporting both static and dynamic provider management.

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

See [Testing Strategy](docs/04-Guidelines/Testing_Strategy.md) for more details.

## 📦 Deployment

```bash
# Docker Compose
docker-compose up -d

# Kubernetes
kubectl apply -f k8s/
```

See [Kubernetes Deployment](docs/03-Infrastructure/Kubernetes_Deployment.md) for detailed deployment instructions.

## 🤝 Contributing

See [Contributing Guide](docs/04-Guidelines/Contributing.md) for development setup and contribution guidelines.

## 📄 License

[Add your license here]

## 🔗 Related Documentation

- [Documentation Index](docs/05-Index.yaml) - Semantic sitemap for AI context routing
- [CHANGELOG](docs/CHANGELOG.md) - Version history and enhancements
