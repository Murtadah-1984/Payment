---
title: Environment Configuration
version: 1.0
last_updated: 2025-01-15
category: Infrastructure
tags:
  - environment
  - configuration
  - development
  - staging
  - production
  - appsettings
summary: >
  Environment-specific configuration guide for Development, Staging, and Production environments.
related_docs:
  - Kubernetes_Deployment.md
  - Observability.md
  - ../00-Overview/README.md
ai_context_priority: high
---

# 🌍 Environment Configuration

The Payment Microservice supports three distinct environments: **Development**, **Staging**, and **Production**. Each environment has its own configuration file that overrides the base `appsettings.json`.

## Environment Files

The application automatically loads environment-specific configuration files based on the `ASPNETCORE_ENVIRONMENT` variable:

```
src/Payment.API/
├── appsettings.json              (base configuration)
├── appsettings.Development.json  (development overrides)
├── appsettings.Staging.json      (staging overrides)
└── appsettings.Production.json    (production overrides)
```

## Configuration Loading Order

ASP.NET Core loads configuration files in the following order (later files override earlier ones):

1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific overrides)
3. Environment variables (highest priority)
4. Command-line arguments (highest priority)

## Environment-Specific Settings

### Development (`Development`)

**Purpose**: Local development and debugging

**Key Characteristics**:
- ✅ **Swagger/GraphQL Tools**: Enabled for API exploration
- ✅ **Detailed Logging**: Debug level with EF Core query logging
- ✅ **Test Mode**: All payment providers in test mode
- ✅ **Relaxed Rate Limits**: Higher limits for development
- ✅ **Local Database**: Default connection to localhost PostgreSQL
- ✅ **Auto Migrations**: Database migrations run automatically on startup

**Configuration Highlights**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=PaymentDb;..."
  }
}
```

**Usage**:
```bash
# Set environment variable
export ASPNETCORE_ENVIRONMENT=Development

# Or use launch profile
dotnet run --launch-profile "https"
```

### Staging (`Staging`)

**Purpose**: Pre-production testing and validation

**Key Characteristics**:
- ✅ **Swagger/GraphQL Tools**: Enabled for testing
- ✅ **Moderate Logging**: Information level
- ✅ **Test Mode**: Payment providers in test mode
- ✅ **Moderate Rate Limits**: Higher than production (20 req/min vs 10)
- ✅ **Environment Variables**: Uses environment variables for sensitive data
- ✅ **OpenTelemetry**: 50% sampling ratio for observability
- ✅ **Feature Flags**: Most features enabled for testing

**Configuration Highlights**:
```json
{
  "IpRateLimiting": {
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/v1/payments",
        "Period": "1m",
        "Limit": 20
      }
    ]
  },
  "OpenTelemetry": {
    "SamplingRatio": 0.5
  }
}
```

**Usage**:
```bash
# Set environment variable
export ASPNETCORE_ENVIRONMENT=Staging

# Or use launch profile
dotnet run --launch-profile "Staging"
```

### Production (`Production`)

**Purpose**: Live production environment

**Key Characteristics**:
- ❌ **Swagger/GraphQL Tools**: Disabled for security
- ✅ **Minimal Logging**: Warning level for framework logs
- ❌ **Test Mode**: Payment providers in live mode
- ✅ **Strict Rate Limits**: Conservative limits (10 req/min)
- ✅ **Environment Variables**: All sensitive data via environment variables
- ✅ **OpenTelemetry**: 10% sampling ratio for cost optimization
- ✅ **Production Logging**: Logs to `/var/log/payment/`
- ✅ **Feature Flags**: Conservative feature rollout

**Configuration Highlights**:
```json
{
  "IpRateLimiting": {
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/v1/payments",
        "Period": "1m",
        "Limit": 10
      }
    ]
  },
  "OpenTelemetry": {
    "SamplingRatio": 0.1,
    "UseConsoleExporter": false
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/payment/payment-.txt"
        }
      }
    ]
  }
}
```

**Usage**:
```bash
# Set environment variable
export ASPNETCORE_ENVIRONMENT=Production

# Or use launch profile
dotnet run --launch-profile "Production"
```

## Launch Profiles

The `launchSettings.json` file includes profiles for all environments:

```json
{
  "profiles": {
    "http": { "ASPNETCORE_ENVIRONMENT": "Development" },
    "https": { "ASPNETCORE_ENVIRONMENT": "Development" },
    "Staging": { "ASPNETCORE_ENVIRONMENT": "Staging" },
    "Production": { "ASPNETCORE_ENVIRONMENT": "Production" }
  }
}
```

## Environment Variables

### Required Variables (Staging/Production)

```bash
# Database
POSTGRES_HOST=postgres-service
POSTGRES_PORT=5432
POSTGRES_DB=PaymentDb
POSTGRES_USER=payment_user
POSTGRES_PASSWORD=<secure-password>

# Authentication
AUTH_AUTHORITY=https://identity.yourdomain.com
AUTH_AUDIENCE=payment-service

# Observability (optional)
JAEGER_AGENT_HOST=jaeger-agent
JAEGER_AGENT_PORT=6831
ZIPKIN_ENDPOINT=http://zipkin:9411/api/v2/spans
OTLP_ENDPOINT=http://otel-collector:4317
```

### Kubernetes Deployment

In Kubernetes, set environment variables via ConfigMaps and Secrets:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: payment-config
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  POSTGRES_HOST: "postgres-service"
  POSTGRES_PORT: "5432"
  POSTGRES_DB: "PaymentDb"
---
apiVersion: v1
kind: Secret
metadata:
  name: payment-secrets
type: Opaque
stringData:
  POSTGRES_USER: "payment_user"
  POSTGRES_PASSWORD: "<secure-password>"
  AUTH_AUTHORITY: "https://identity.yourdomain.com"
  AUTH_AUDIENCE: "payment-service"
```

## Environment Detection in Code

The application uses `IWebHostEnvironment` to detect the current environment:

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
{
    // Auto-migrate database
    dbContext.Database.Migrate();
}
```

## Best Practices

1. **Never commit secrets** to `appsettings.*.json` files
2. **Use environment variables** for all sensitive data in Staging/Production
3. **Test configuration changes** in Staging before Production
4. **Use feature flags** for gradual feature rollout
5. **Monitor rate limits** and adjust based on traffic patterns
6. **Review logging levels** regularly to balance observability and cost
7. **Validate environment** on application startup

## Troubleshooting

### Configuration Not Loading

- Verify `ASPNETCORE_ENVIRONMENT` is set correctly
- Check file naming: `appsettings.{Environment}.json` (case-sensitive)
- Ensure files are included in the project build

### Environment Variables Not Overriding

- Environment variables use double underscore (`__`) for nested properties
- Example: `ConnectionStrings__DefaultConnection` not `ConnectionStrings:DefaultConnection`

### Swagger Not Showing

- Swagger is disabled in Production for security
- Use Development or Staging environment to access Swagger

## See Also

- [Kubernetes Deployment](Kubernetes_Deployment.md)
- [Observability & Monitoring](Observability.md)
- [Overview](../00-Overview/README.md)

