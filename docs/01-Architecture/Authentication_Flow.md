---
title: Authentication & Security Flow
version: 1.0
last_updated: 2025-11-11
category: Architecture
tags:
  - authentication
  - jwt
  - security
  - oauth2
  - oidc
  - secrets-management
summary: >
  Complete authentication and security documentation covering JWT authentication with external Identity Microservice,
  secrets management, webhook validation, and security best practices.
related_docs:
  - System_Architecture.md
  - ../02-Payment/Security_Policy.md
ai_context_priority: high
---

# 🔐 Authentication & Security

## JWT Authentication with External Identity Microservice

The Payment Microservice uses **JWT Bearer Token** authentication with an **external Identity Microservice** following **OpenID Connect (OIDC)** and **OAuth2** standards. This eliminates the need for local JWT secret keys and centralizes authentication management.

### Architecture Overview

```
┌─────────────────┐         ┌──────────────────┐         ┌─────────────────┐
│   API Gateway   │────────▶│ Identity Service │────────▶│ Payment Service │
│  (Kong/YARP)    │  JWT    │  (OIDC/OAuth2)   │  JWKS   │  (Validates)    │
└─────────────────┘         └──────────────────┘         └─────────────────┘
     │                              │                              │
     │ Validates JWT                │ Issues Tokens                │ Validates via JWKS
     │ Injects Claims               │ Publishes JWKS               │ No local secrets
     └──────────────────────────────┴──────────────────────────────┘
```

### Configuration

Configure the Identity Microservice authority and audience in `appsettings.json`:

```json
{
  "Auth": {
    "Authority": "https://identity.yourdomain.com",
    "Audience": "payment-service"
  }
}
```

**Key Points:**
- **No local secret keys** - Tokens are validated using public keys from the Identity Microservice's JWKS endpoint
- **Centralized authentication** - All microservices validate tokens against the same Identity Microservice
- **OIDC/OAuth2 compliant** - Follows industry standards for token validation
- **Stateless and scalable** - No shared state between services

### Identity Microservice Requirements

The Identity Microservice must expose:

1. **OpenID Connect Discovery Endpoint:**
   ```
   GET https://identity.yourdomain.com/.well-known/openid-configuration
   ```

2. **JWKS Endpoint** (for public key validation):
   ```
   GET https://identity.yourdomain.com/.well-known/jwks.json
   ```

### Authorization Policies

The Payment Microservice implements fine-grained authorization policies based on token scopes:

- **`PaymentsWrite`** - Requires `scope: payment.write` claim
- **`PaymentsRead`** - Requires `scope: payment.read` claim
- **`PaymentsAdmin`** - Requires `scope: payment.admin` claim

Apply policies to controllers using the `[Authorize(Policy = "PaymentsWrite")]` attribute.

### Service-to-Service Communication

For backend microservices (e.g., Order → Payment), use the **Client Credentials Flow**:

1. Register each service in the Identity Microservice with a unique `client_id` and `client_secret`
2. Obtain tokens using:
   ```
   POST /connect/token
   grant_type=client_credentials
   client_id=order-service
   client_secret=***
   scope=payment.write
   ```
3. Include the token in the `Authorization: Bearer <token>` header

### API Gateway Configuration

At the API Gateway level (Kong, YARP, NGINX Ingress, or Istio), configure:

- **JWT/OIDC plugin** enabled
- **Validation** against the same Authority: `https://identity.yourdomain.com`
- **Audience** set to `payment-service`
- **Claim forwarding** as headers:
  - `X-User-Id`
  - `X-Email`
  - `X-Roles`

The gateway performs first-level token validation and forwards validated requests to the Payment Microservice.

### Kubernetes Configuration

In Kubernetes, configure via environment variables:

```yaml
env:
  - name: Auth__Authority
    value: "https://identity.yourdomain.com"
  - name: Auth__Audience
    value: "payment-service"
```

**Note:** No `JwtSettings__SecretKey` is required - authentication is handled by the external Identity Microservice.

## 🔐 Secrets Management (CRITICAL Security Feature)

The Payment Microservice implements **enterprise-grade secrets management** to securely store and retrieve sensitive configuration data such as API keys, connection strings, and encryption keys.

### Supported Secrets Providers

The microservice supports multiple secrets management providers:

1. **Azure Key Vault** - For Azure cloud deployments
2. **AWS Secrets Manager** - For AWS cloud deployments
3. **Kubernetes Secrets** - For Kubernetes deployments (via environment variables)
4. **Configuration** - For development/fallback (appsettings.json, environment variables)

### Configuration

Configure the secrets provider in `appsettings.json`:

```json
{
  "SecretsManagement": {
    "Provider": "configuration"
  }
}
```

**Provider Options:**
- `"configuration"` - Use appsettings.json and environment variables (development)
- `"azure-keyvault"` or `"azure"` - Use Azure Key Vault
- `"aws-secretsmanager"` or `"aws"` - Use AWS Secrets Manager
- `"kubernetes"` or `"k8s"` - Use Kubernetes Secrets (via environment variables)

### Azure Key Vault Setup

1. **Create Key Vault** in Azure Portal
2. **Configure Authentication** - Uses `DefaultAzureCredential` which supports:
   - Managed Identity (for Azure services)
   - Azure CLI (for local development)
   - Visual Studio credentials
   - Environment variables

3. **Add Configuration:**
```json
{
  "SecretsManagement": {
    "Provider": "azure-keyvault"
  },
  "KeyVault": {
    "Uri": "https://your-keyvault.vault.azure.net/",
    "KeyPrefix": "Payment" // Optional: prefix for all secret names
  }
}
```

4. **Store Secrets** in Key Vault:
   - Secret names use format: `{KeyPrefix}-{ConfigKey}` (e.g., `Payment-ConnectionStrings-DefaultConnection`)
   - Or without prefix: `ConnectionStrings-DefaultConnection`
   - **Note:** JWT authentication is now handled by external Identity Microservice - no JWT secret keys needed

### AWS Secrets Manager Setup

1. **Create Secrets** in AWS Secrets Manager
2. **Configure IAM Permissions** - Ensure the application has `secretsmanager:GetSecretValue` permission
3. **Add Configuration:**
```json
{
  "SecretsManagement": {
    "Provider": "aws-secretsmanager"
  },
  "AwsSecretsManager": {
    "SecretPrefix": "Payment" // Optional: prefix for all secret names
  }
}
```

4. **Store Secrets** in Secrets Manager:
   - Secret names use format: `{SecretPrefix}/{ConfigKey}` (e.g., `Payment/ConnectionStrings/DefaultConnection`)
   - Or without prefix: `ConnectionStrings/DefaultConnection`
   - **Note:** JWT authentication is now handled by external Identity Microservice - no JWT secret keys needed

### Kubernetes Secrets Setup

1. **Create Kubernetes Secret:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: payment-secrets
  namespace: payment
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=postgres;Database=PaymentDb;..."
  PaymentProviders__ZainCash__MerchantSecret: "zaincash-secret"
  # Note: JWT authentication is handled by external Identity Microservice
  # No JwtSettings__SecretKey is required
```

2. **Mount Secret as Environment Variables** in deployment:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: payment-api
spec:
  template:
    spec:
      containers:
      - name: payment-api
        envFrom:
        - secretRef:
            name: payment-secrets
```

3. **Use External Secrets Operator** (Recommended for production):
```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: payment-secrets
spec:
  secretStoreRef:
    name: azure-keyvault
  target:
    name: payment-secrets
  data:
    # JWT authentication is handled by external Identity Microservice
    # No JwtSettings__SecretKey is required
    - secretKey: ConnectionStrings__DefaultConnection
      remoteRef:
        key: Payment-ConnectionStrings-DefaultConnection
```

### Secrets Loaded Automatically

The following secrets are automatically loaded from the configured provider:

- `ConnectionStrings:DefaultConnection` - Database connection string
- `DataEncryption:Key` - AES-256 encryption key for metadata
- `PaymentProviders:{Provider}:{SecretField}` - Payment provider API keys and secrets

**Note:** JWT authentication is now handled by external Identity Microservice - no `JwtSettings:SecretKey` is required.
  - Examples:
    - `PaymentProviders:ZainCash:MerchantSecret`
    - `PaymentProviders:ZainCash:WebhookSecret`
    - `PaymentProviders:Stripe:ApiKey`
    - `PaymentProviders:FIB:ClientSecret`

### Architecture

The secrets management follows **Clean Architecture** and **SOLID principles**:

- **Domain Layer**: `ISecretsManager` interface (Dependency Inversion Principle)
- **Infrastructure Layer**: Implementations (Azure, AWS, Configuration, Kubernetes)
- **Factory Pattern**: `SecretsManagerFactory` selects the appropriate implementation
- **Strategy Pattern**: Each provider is a strategy implementation

### Security Best Practices

1. **Never Commit Secrets** to version control
   - Use `.gitignore` for `appsettings.Production.json`
   - Use secret scanning tools (GitHub Secret Scanning, GitGuardian)

2. **Use Managed Identities** (Azure) or **IAM Roles** (AWS)
   - No passwords or access keys in configuration
   - Automatic credential rotation

3. **Rotate Secrets Regularly**
   - JWT keys: Quarterly
   - API keys: As per provider policy
   - Database passwords: Quarterly

4. **Separate Secrets by Environment**
   - Different Key Vaults/Secrets Manager for dev, staging, production
   - Use different prefixes or namespaces

5. **Audit Secret Access**
   - Enable Key Vault logging
   - Monitor AWS CloudTrail for Secrets Manager access
   - Review access logs regularly

6. **Use Least Privilege**
   - Grant only necessary permissions
   - Use separate service principals/roles per environment

### Development vs Production

**Development:**
- Use `"Provider": "configuration"` (appsettings.json)
- Store non-sensitive test values in `appsettings.Development.json`
- Never commit real secrets

**Production:**
- Use `"Provider": "azure-keyvault"` or `"aws-secretsmanager"`
- Store all secrets in Key Vault/Secrets Manager
- Use Managed Identity/IAM Roles for authentication
- Enable audit logging

### Testing

Comprehensive unit tests ensure secrets management works correctly:
- ✅ Configuration provider tests
- ✅ Factory pattern tests
- ✅ Configuration provider integration tests
- ✅ Error handling tests

### Migration Guide

To migrate from configuration-based secrets to Key Vault/Secrets Manager:

1. **Create Key Vault/Secrets Manager** in your cloud provider
2. **Store Secrets** using the naming convention above
3. **Update Configuration** to use the new provider
4. **Test** in staging environment first
5. **Deploy** to production with monitoring
6. **Remove Secrets** from appsettings.json (keep placeholders for documentation)

## See Also

- [System Architecture](System_Architecture.md)
- [Security Policy](../02-Payment/Security_Policy.md)
- [Kubernetes Deployment](../03-Infrastructure/Kubernetes_Deployment.md)

