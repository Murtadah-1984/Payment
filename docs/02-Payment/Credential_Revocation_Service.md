---
title: Credential Revocation Service
version: 1.0
last_updated: 2025-01-15
category: Payment
tags:
  - security
  - credential-revocation
  - jwt
  - api-keys
  - secret-rotation
  - kubernetes
  - blacklist
summary: >
  Comprehensive credential revocation service for API keys, JWT tokens, and secrets with
  distributed cache for fast lookups, database audit trail, and Kubernetes secret rotation support.
related_docs:
  - Security_Policy.md
  - Security_Incident_Response_Service.md
  - ../01-Architecture/Authentication_Flow.md
  - ../03-Infrastructure/Kubernetes_Deployment.md
ai_context_priority: high
---

# 🔐 Credential Revocation Service

The Payment Microservice includes a comprehensive **Credential Revocation Service** that provides secure revocation of compromised credentials, including API keys, JWT tokens, and secrets. The service features distributed cache for fast lookups, database audit trail, secret rotation support, and JWT token blacklist middleware.

## Overview

The Credential Revocation Service is designed to ensure security by:

1. **Revoking Compromised Credentials** - Immediately revoke API keys, JWT tokens, and secrets
2. **Fast Lookup** - Distributed cache (Redis) for O(1) revocation checks
3. **Audit Trail** - Database persistence for compliance and audit requirements
4. **Secret Rotation** - Support for rotating secrets (database connections, payment provider keys, etc.)
5. **JWT Blacklist** - Middleware to check JWT tokens against revocation list
6. **Kubernetes Integration** - Structure for Kubernetes secret rotation

## Features

- ✅ **API Key Revocation** - Revoke compromised API keys immediately
- ✅ **JWT Token Revocation** - Blacklist JWT tokens by token ID (JTI claim)
- ✅ **Secret Rotation** - Rotate secrets for database connections, payment providers, JWT signing keys, webhooks
- ✅ **Distributed Cache** - Redis cache for fast revocation checks
- ✅ **Database Audit Trail** - PostgreSQL persistence for compliance
- ✅ **JWT Blacklist Middleware** - Automatic token validation against revocation list
- ✅ **TTL Management** - Configurable TTL based on credential type
- ✅ **Bulk Operations** - Support for revoking multiple credentials
- ✅ **Admin Endpoints** - RESTful API for credential management
- ✅ **Kubernetes Support** - Structure for K8s secret rotation
- ✅ **Stateless Design** - Suitable for Kubernetes horizontal scaling

## Architecture

### Components

1. **ICredentialRevocationService** (`Payment.Domain.Interfaces.ICredentialRevocationService`)
   - Main interface for credential revocation operations
   - Located in Domain layer following Clean Architecture

2. **CredentialRevocationService** (`Payment.Infrastructure.Security.CredentialRevocationService`)
   - Core implementation of credential revocation logic
   - Handles cache and database operations

3. **RevokedCredential** (`Payment.Domain.Entities.RevokedCredential`)
   - Entity for storing revoked credentials in database
   - Provides audit trail with revocation reason, timestamp, and user

4. **JwtTokenBlacklistMiddleware** (`Payment.Infrastructure.Security.JwtTokenBlacklistMiddleware`)
   - Middleware to check JWT tokens against revocation list
   - Integrated into ASP.NET Core request pipeline

5. **KubernetesSecretRotationService** (`Payment.Infrastructure.Security.KubernetesSecretRotationService`)
   - Service for rotating Kubernetes secrets
   - Requires Kubernetes client library for full implementation

### Data Models

#### CredentialType

```csharp
public enum CredentialType
{
    ApiKey = 0,
    JwtToken = 1,
    OAuth2Token = 2,
    WebhookSecret = 3,
    DatabaseConnection = 4,
    PaymentProviderKey = 5,
    JwtSigningKey = 6,
    Other = 99
}
```

#### RevokedCredential

```csharp
public class RevokedCredential
{
    public string CredentialId { get; set; }
    public CredentialType Type { get; set; }
    public DateTime RevokedAt { get; set; }
    public string Reason { get; set; }
    public string? RevokedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
```

## Usage

### Revoking API Keys

```csharp
public class SecurityService
{
    private readonly ICredentialRevocationService _revocationService;

    public SecurityService(ICredentialRevocationService revocationService)
    {
        _revocationService = revocationService;
    }

    public async Task RevokeCompromisedApiKey(string apiKeyId)
    {
        await _revocationService.RevokeApiKeyAsync(
            apiKeyId,
            CancellationToken.None);
    }
}
```

### Revoking JWT Tokens

```csharp
// Extract token ID (JTI claim) from JWT
var handler = new JwtSecurityTokenHandler();
var token = handler.ReadJwtToken(jwtToken);
var tokenId = token.Id; // JTI claim

await _revocationService.RevokeJwtTokenAsync(
    tokenId,
    CancellationToken.None);
```

### Checking Revocation Status

```csharp
var isRevoked = await _revocationService.IsRevokedAsync(
    credentialId: "api-key-123",
    CancellationToken.None);

if (isRevoked)
{
    // Reject the request
    return Unauthorized();
}
```

### Rotating Secrets

```csharp
// Rotate database connection string
await _revocationService.RotateSecretsAsync(
    secretName: "database-connection-string",
    CancellationToken.None);

// Rotate payment provider API key
await _revocationService.RotateSecretsAsync(
    secretName: "payment-provider-stripe-api-key",
    CancellationToken.None);

// Rotate JWT signing key
await _revocationService.RotateSecretsAsync(
    secretName: "jwt-signing-key",
    CancellationToken.None);
```

### Getting Revoked Credentials

```csharp
// Get all revoked credentials
var allRevoked = await _revocationService.GetRevokedCredentialsAsync(
    since: null,
    CancellationToken.None);

// Get credentials revoked in the last 24 hours
var recentRevoked = await _revocationService.GetRevokedCredentialsAsync(
    since: DateTime.UtcNow.AddDays(-1),
    CancellationToken.None);
```

## JWT Token Blacklist Middleware

The JWT token blacklist middleware automatically checks all incoming JWT tokens against the revocation list:

### How It Works

1. Middleware extracts JWT token from `Authorization` header
2. Extracts token ID (JTI claim) from the JWT
3. Checks if token ID is in the revocation list
4. If revoked, returns 401 Unauthorized
5. If not revoked, continues to next middleware

### Integration

The middleware is automatically registered in `Program.cs`:

```csharp
app.UseAuthentication();

// JWT token blacklist middleware (must be after authentication to extract token)
app.UseMiddleware<JwtTokenBlacklistMiddleware>();

app.UseAuthorization();
```

### Token ID Extraction

The middleware extracts the JTI (JWT ID) claim from the token:

```csharp
var handler = new JwtSecurityTokenHandler();
var jwtToken = handler.ReadJwtToken(token);
var tokenId = jwtToken.Id; // JTI claim
```

**Important**: Ensure your JWT tokens include a JTI claim when issued.

## Admin API Endpoints

### Revoke API Key

```http
POST /api/v1/admin/credentials/api-keys/{apiKeyId}/revoke
Authorization: Bearer {admin-token}
```

**Response**: `204 No Content`

### Revoke JWT Token

```http
POST /api/v1/admin/credentials/jwt-tokens/{tokenId}/revoke
Authorization: Bearer {admin-token}
```

**Response**: `204 No Content`

### Rotate Secret

```http
POST /api/v1/admin/credentials/secrets/{secretName}/rotate
Authorization: Bearer {admin-token}
```

**Response**: `204 No Content`

### Check Revocation Status

```http
GET /api/v1/admin/credentials/check/{credentialId}
Authorization: Bearer {admin-token}
```

**Response**:
```json
true
```

### Get Revoked Credentials

```http
GET /api/v1/admin/credentials?since=2025-01-01T00:00:00Z
Authorization: Bearer {admin-token}
```

**Response**:
```json
[
  {
    "credentialId": "api-key-123",
    "type": "ApiKey",
    "revokedAt": "2025-01-15T10:30:00Z",
    "reason": "Revoked via API",
    "revokedBy": "admin@example.com",
    "expiresAt": null
  }
]
```

## Database Schema

### RevokedCredentials Table

```sql
CREATE TABLE "RevokedCredentials" (
    "CredentialId" VARCHAR(255) NOT NULL PRIMARY KEY,
    "Type" VARCHAR(50) NOT NULL,
    "RevokedAt" TIMESTAMP NOT NULL,
    "Reason" VARCHAR(500) NOT NULL,
    "RevokedBy" VARCHAR(255),
    "ExpiresAt" TIMESTAMP,
    INDEX "IX_RevokedCredentials_RevokedAt" ("RevokedAt"),
    INDEX "IX_RevokedCredentials_Type" ("Type")
);
```

## Cache Strategy

### TTL by Credential Type

- **JWT Tokens**: 30 days (matches typical JWT expiration)
- **API Keys**: 90 days (longer-lived credentials)
- **Other**: 30 days (default)

### Cache Key Format

```
revoked:{credentialId}
```

### Fallback Strategy

1. First check distributed cache (Redis)
2. If not found, check database
3. If found in database, cache result for future lookups

## Secret Rotation

### Supported Secret Types

The service automatically determines credential type from secret name:

- **Database Connection**: Contains "database" or "connection"
- **Payment Provider Key**: Contains "payment" and "provider"
- **JWT Signing Key**: Contains "jwt" and "signing"
- **Webhook Secret**: Contains "webhook"
- **Other**: Default fallback

### Rotation Process

1. **Revoke Old Secret**: Old secret is added to revocation list
2. **Generate New Secret**: New secret value is generated (requires secrets manager integration)
3. **Update Kubernetes Secret**: Secret is updated in Kubernetes (requires K8s client)
4. **Notify Services**: Dependent services are notified to reload secrets

### Kubernetes Secret Rotation

The `KubernetesSecretRotationService` provides structure for K8s secret rotation:

```csharp
public interface IKubernetesSecretRotationService
{
    Task RotateSecretAsync(
        string secretName,
        string @namespace = "default",
        CancellationToken cancellationToken = default);
    
    Task<bool> IsSecretRotationInProgressAsync(
        string secretName,
        CancellationToken cancellationToken = default);
}
```

**Note**: Full implementation requires Kubernetes client library (e.g., `KubernetesClient`).

## Security Considerations

### Audit Trail

All revocation operations are logged to the database with:
- Credential ID
- Credential type
- Revocation timestamp
- Reason for revocation
- User who performed revocation

### Immediate Effect

- **Cache**: Revoked credentials are immediately available in cache
- **Database**: Persisted for audit trail
- **JWT Middleware**: Checks every request automatically

### Multi-Region Support

For multi-region deployments:
- Use distributed cache (Redis) with replication
- Database replication for audit trail
- Consider eventual consistency for cache propagation

## Configuration

### Redis Connection

Configure Redis connection in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### Database Configuration

The `RevokedCredentials` table is automatically created via EF Core migrations:

```bash
dotnet ef migrations add AddRevokedCredentials
dotnet ef database update
```

## Testing

Unit tests are available in `tests/Payment.Infrastructure.Tests/Security/CredentialRevocationServiceTests.cs`:

- API key revocation
- JWT token revocation
- Secret rotation
- Revocation status checking
- Database persistence
- Date filtering

## Best Practices

1. **Revoke Immediately**: Revoke compromised credentials as soon as possible
2. **Use JTI Claims**: Ensure JWT tokens include JTI claims for blacklist support
3. **Monitor Revocations**: Set up alerts for unusual revocation patterns
4. **Regular Audits**: Review revoked credentials regularly for security analysis
5. **Secret Rotation**: Rotate secrets regularly, not just when compromised
6. **TTL Management**: Adjust TTL based on credential lifetime

## Troubleshooting

### JWT Tokens Not Being Rejected

1. Verify JWT tokens include JTI claim
2. Check middleware is registered in correct order (after authentication)
3. Verify token ID extraction is working correctly
4. Check cache and database for revoked token

### Cache Not Working

1. Verify Redis connection string is correct
2. Check Redis is accessible from application
3. Check fallback to database is working
4. Review cache TTL settings

### Secret Rotation Not Working

1. Verify secrets manager integration
2. Check Kubernetes client library is installed
3. Verify RBAC permissions for secret updates
4. Review rotation logs for errors

## Related Documentation

- [Security Policy](./Security_Policy.md) - Security features and compliance
- [Security Incident Response Service](./Security_Incident_Response_Service.md) - Security incident handling
- [Authentication Flow](../01-Architecture/Authentication_Flow.md) - JWT authentication
- [Kubernetes Deployment](../03-Infrastructure/Kubernetes_Deployment.md) - Deployment configuration

