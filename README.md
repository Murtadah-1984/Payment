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
