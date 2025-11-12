---
title: Security Incident Response Service
version: 1.0
last_updated: 2025-01-15
category: Payment
tags:
  - security
  - incident-response
  - security-incidents
  - containment
  - credential-revocation
  - audit-logging
  - admin-endpoints
summary: >
  Comprehensive security incident response system that automatically assesses security events,
  contains threats, revokes compromised credentials, generates incident reports, and provides
  admin endpoints for security incident management.
related_docs:
  - Incident_Response_Service.md
  - Security_Policy.md
  - ../01-Architecture/Authentication_Flow.md
  - ../03-Infrastructure/Observability.md
ai_context_priority: high
---

# 🔒 Security Incident Response Service

The Payment Microservice includes a comprehensive **Security Incident Response Service** that automatically detects, assesses, contains, and responds to security incidents. The service provides automated threat assessment, containment strategies, credential revocation, incident reporting, and secure admin endpoints for security operations.

## Overview

The Security Incident Response Service is designed to handle security incidents proactively by:

1. **Assessing Security Events** - Analyzes security events to determine severity, threat type, and recommended containment strategies
2. **Containing Threats** - Executes containment strategies (IP blocking, credential revocation, pod isolation, etc.)
3. **Revoking Credentials** - Automatically revokes compromised API keys and JWT tokens
4. **Generating Reports** - Creates comprehensive incident reports for security teams
5. **Admin Endpoints** - Provides secure API endpoints for security incident management

## Features

- ✅ **Automated Security Assessment** - Intelligent severity determination based on event type and related events
- ✅ **Threat Type Classification** - Categorizes threats (Credential Attack, Unauthorized Access, Data Exfiltration, etc.)
- ✅ **Containment Strategies** - Multiple containment options (IsolatePod, BlockIpAddress, RevokeCredentials, etc.)
- ✅ **Credential Revocation** - Automatic revocation of compromised API keys and JWT tokens
- ✅ **Security Alert Notifications** - Sends alerts to security team based on incident severity
- ✅ **Incident Reporting** - Generates detailed JSON reports with event timeline and remediation actions
- ✅ **Audit Log Integration** - Queries security events from audit logs for analysis
- ✅ **Stateless Design** - Suitable for Kubernetes horizontal scaling
  - **Database Persistence** - All security incidents are persisted to PostgreSQL database
  - **Repository Pattern** - Uses `ISecurityIncidentRepository` for data access
  - **No In-Memory State** - Removed static dictionaries, all state stored in database
  - **Distributed Cache** - Circuit breaker states use Redis for shared state across pods
- ✅ **Comprehensive Logging** - Structured logging for observability and audit trails
- ✅ **Admin API Endpoints** - Secure REST endpoints for security incident management
- ✅ **IP Whitelisting** - Production-ready IP whitelisting for admin endpoints
- ✅ **Rate Limiting** - Admin endpoints protected with 100 req/min rate limiting
- ✅ **Request/Response Logging** - Complete audit trail for all admin actions

## Architecture

### Components

1. **ISecurityIncidentResponseService** (`Payment.Application.Services.ISecurityIncidentResponseService`)
   - Main interface for security incident response operations
   - Located in Application layer following Clean Architecture

2. **SecurityIncidentResponseService** (`Payment.Application.Services.SecurityIncidentResponseService`)
   - Core implementation of security incident response logic
   - Orchestrates assessment, containment, and reporting
   - Uses `ISecurityIncidentRepository` for stateless incident tracking
   - Persists all incidents to database (no in-memory state)

3. **IAuditLogger** (`Payment.Domain.Interfaces.IAuditLogger`)
   - Interface for logging and querying security events
   - Implemented in Infrastructure layer

4. **AuditLogger** (`Payment.Infrastructure.Security.AuditLogger`)
   - Logs security events to audit log repository
   - Queries related security events for incident analysis

5. **ICredentialRevocationService** (`Payment.Domain.Interfaces.ICredentialRevocationService`)
   - Interface for revoking compromised credentials
   - Implemented in Infrastructure layer

6. **CredentialRevocationService** (`Payment.Infrastructure.Security.CredentialRevocationService`)
   - Revokes API keys and JWT tokens
   - Uses distributed cache (Redis) for revocation tracking
   - Stores revoked credentials with TTL

7. **ISecurityNotificationService** (`Payment.Domain.Interfaces.ISecurityNotificationService`)
   - Interface for sending security alerts
   - Implemented in Infrastructure layer

8. **SecurityNotificationService** (`Payment.Infrastructure.Security.SecurityNotificationService`)
   - Sends security alerts to security team
   - Supports multiple notification channels (email, Slack, PagerDuty, etc.)

9. **SecurityIncidentController** (`Payment.API.Controllers.Admin.SecurityIncidentController`)
   - REST API controller for security incident management
   - Requires `SecurityAdminOnly` authorization policy

10. **ISecurityIncidentRepository** (`Payment.Domain.Interfaces.ISecurityIncidentRepository`)
    - Repository interface for security incident data access
    - Extends `IRepository<SecurityIncident>` for CRUD operations
    - Provides `GetByIncidentIdAsync` for incident lookup

11. **SecurityIncidentRepository** (`Payment.Infrastructure.Repositories.SecurityIncidentRepository`)
    - Repository implementation for security incident persistence
    - Uses Entity Framework Core with PostgreSQL
    - Stateless design - all state persisted to database

12. **SecurityIncident** (`Payment.Domain.Entities.SecurityIncident`)
    - Domain entity representing a security incident
    - Persisted to `SecurityIncidents` table in database
    - Contains security event data, assessment, and containment status

13. **IncidentController** (`Payment.API.Controllers.Admin.IncidentController`)
    - REST API controller for payment failure incident management
    - Requires `AdminOnly` authorization policy

14. **AdminRequestLoggingMiddleware** (`Payment.API.Middleware.AdminRequestLoggingMiddleware`)
    - Logs all requests and responses to admin endpoints
    - Provides complete audit trail for administrative actions

15. **IpWhitelistMiddleware** (`Payment.API.Middleware.IpWhitelistMiddleware`)
    - Enforces IP whitelisting on admin endpoints in production
    - Supports exact IP matches and CIDR notation

### Data Models

#### SecurityEvent

Value object representing a security event:

```csharp
public sealed record SecurityEvent(
    SecurityEventType EventType,
    DateTime Timestamp,
    string? UserId,
    string? IpAddress,
    string Resource,
    string Action,
    bool Succeeded,
    string? Details);
```

**Properties:**
- `EventType` - Type of security event (AuthenticationFailure, UnauthorizedAccess, etc.)
- `Timestamp` - When the event occurred
- `UserId` - User ID associated with the event (if applicable)
- `IpAddress` - Source IP address
- `Resource` - Resource that was accessed
- `Action` - Action that was performed
- `Succeeded` - Whether the action succeeded
- `Details` - Additional details about the event

#### SecurityIncidentAssessment

DTO representing an assessment of a security incident:

```csharp
public sealed record SecurityIncidentAssessment(
    SecurityIncidentSeverity Severity,
    SecurityThreatType ThreatType,
    IEnumerable<string> AffectedResources,
    IEnumerable<string> CompromisedCredentials,
    ContainmentStrategy RecommendedContainment,
    IEnumerable<RemediationAction> RemediationActions);
```

**Properties:**
- `Severity` - Severity level (Low, Medium, High, Critical)
- `ThreatType` - Type of threat (CredentialAttack, UnauthorizedAccess, etc.)
- `AffectedResources` - List of affected resources
- `CompromisedCredentials` - List of compromised credential IDs
- `RecommendedContainment` - Recommended containment strategy
- `RemediationActions` - List of recommended remediation actions

#### SecurityIncidentId

Value object for security incident identifiers:

```csharp
public sealed record SecurityIncidentId(Guid Value);
```

#### ContainmentStrategy

Enumeration of containment strategies:

```csharp
public enum ContainmentStrategy
{
    IsolatePod,          // Isolate the affected Kubernetes pod
    BlockIpAddress,      // Block the source IP address
    RevokeCredentials,   // Revoke compromised credentials
    DisableFeature,      // Disable a specific feature or endpoint
    ScaleDown,           // Scale down the affected service
    NetworkIsolation     // Isolate the affected service from the network
}
```

#### SecurityEventType

Enumeration of security event types:

```csharp
public enum SecurityEventType
{
    AuthenticationFailure,      // Failed authentication attempt
    SuspiciousAuthentication,   // Successful authentication from suspicious location
    UnauthorizedAccess,         // Unauthorized access attempt
    RateLimitExceeded,          // Rate limit exceeded
    SuspiciousPaymentPattern,  // Suspicious payment pattern detected
    CredentialCompromise,       // API key compromised or revoked
    DataBreach,                 // Data breach or unauthorized data access
    MaliciousPayload,           // Malicious payload detected in request
    DDoS,                       // Distributed denial of service attack
    Other                       // Other security-related event
}
```

#### SecurityIncidentSeverity

Enumeration of security incident severity levels:

```csharp
public enum SecurityIncidentSeverity
{
    Low,      // Minor security events with minimal impact
    Medium,   // Moderate security impact requiring attention
    High,     // Significant security impact requiring immediate attention
    Critical  // Severe security impact requiring immediate escalation
}
```

#### SecurityThreatType

Enumeration of security threat types:

```csharp
public enum SecurityThreatType
{
    CredentialAttack,    // Credential-based attack (brute force, credential stuffing)
    UnauthorizedAccess,  // Unauthorized access attempt
    DataExfiltration,   // Data exfiltration or breach
    DenialOfService,     // Denial of service attack
    Malware,             // Malware or malicious code execution
    PaymentFraud,        // Payment fraud or suspicious transaction pattern
    InsiderThreat,       // Insider threat or unauthorized internal access
    Unknown              // Unknown or unclassified threat
}
```

## API Endpoints

### Security Incident Endpoints

All security incident endpoints require `SecurityAdminOnly` authorization policy.

#### Assess Security Incident

Assesses a security event and provides recommendations.

**Endpoint:** `POST /api/v1/admin/security/incidents/assess`

**Authorization:** `SecurityAdminOnly` policy (requires `payment.admin` scope and `SecurityAdmin` role)

**Request Body:**
```json
{
  "eventType": "UnauthorizedAccess",
  "timestamp": "2025-01-15T10:30:00Z",
  "userId": "user123",
  "ipAddress": "192.168.1.100",
  "resource": "/api/admin/payments",
  "action": "UnauthorizedAccess",
  "succeeded": false,
  "details": "Unauthorized access attempt to admin endpoint"
}
```

**Response:** `200 OK`
```json
{
  "severity": "High",
  "threatType": "UnauthorizedAccess",
  "affectedResources": ["/api/admin/payments"],
  "compromisedCredentials": [],
  "recommendedContainment": "BlockIpAddress",
  "remediationActions": [
    {
      "action": "BlockIpAddress",
      "description": "Block IP address: 192.168.1.100",
      "priority": "High",
      "estimatedTime": "5 minutes"
    },
    {
      "action": "NotifySecurityTeam",
      "description": "Security incident detected: UnauthorizedAccess on /api/admin/payments",
      "priority": "High",
      "estimatedTime": "Immediate"
    }
  ]
}
```

#### Contain Security Incident

Contains a security incident using the specified strategy.

**Endpoint:** `POST /api/v1/admin/security/incidents/{incidentId}/contain`

**Authorization:** `SecurityAdminOnly` policy

**Path Parameters:**
- `incidentId` - Security incident ID (GUID)

**Request Body:**
```json
{
  "strategy": "BlockIpAddress",
  "reason": "Security threat detected"
}
```

**Response:** `204 No Content`

#### Get Incident Report

Generates a comprehensive incident report.

**Endpoint:** `GET /api/v1/admin/security/incidents/{incidentId}/report`

**Authorization:** `SecurityAdminOnly` policy

**Path Parameters:**
- `incidentId` - Security incident ID (GUID)

**Response:** `200 OK`
```json
{
  "incidentId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2025-01-15T10:30:00Z",
  "containedAt": "2025-01-15T10:35:00Z",
  "containmentStrategy": "BlockIpAddress",
  "securityEvent": {
    "eventType": "UnauthorizedAccess",
    "timestamp": "2025-01-15T10:30:00Z",
    "userId": "user123",
    "ipAddress": "192.168.1.100",
    "resource": "/api/admin/payments",
    "action": "UnauthorizedAccess",
    "succeeded": false,
    "details": "Unauthorized access attempt"
  },
  "assessment": {
    "severity": "High",
    "threatType": "UnauthorizedAccess",
    "affectedResources": ["/api/admin/payments"],
    "compromisedCredentials": [],
    "recommendedContainment": "BlockIpAddress",
    "remediationActions": [...]
  },
  "relatedEvents": [...],
  "summary": {
    "totalRelatedEvents": 5,
    "timeToContain": "00:05:00"
  }
}
```

#### Revoke Credentials

Revokes compromised credentials.

**Endpoint:** `POST /api/v1/admin/security/incidents/credentials/revoke`

**Authorization:** `SecurityAdminOnly` policy

**Request Body:**
```json
{
  "credentialId": "api-key-123",
  "credentialType": "ApiKey",
  "reason": "Security incident",
  "revokedBy": "admin"
}
```

**Response:** `204 No Content`

### Payment Failure Incident Endpoints

All payment failure incident endpoints require `AdminOnly` authorization policy.

#### Assess Payment Failure

Assesses a payment failure incident and provides recommendations.

**Endpoint:** `POST /api/v1/admin/incidents/payment-failure/assess`

**Authorization:** `AdminOnly` policy (requires `payment.admin` scope)

**Request Body:**
```json
{
  "provider": "Stripe",
  "failureType": "ProviderError",
  "startTime": "2025-01-15T10:00:00Z",
  "endTime": "2025-01-15T10:30:00Z",
  "metadata": {
    "error": "timeout"
  }
}
```

**Response:** `200 OK`
```json
{
  "severity": "High",
  "rootCause": "Payment provider returned an error response",
  "affectedProviders": ["Stripe"],
  "affectedPaymentCount": 50,
  "estimatedResolutionTime": "00:15:00",
  "recommendedActions": [
    {
      "action": "ContactProvider",
      "description": "Contact payment provider support for resolution",
      "priority": "Medium",
      "estimatedTime": "30 minutes"
    }
  ]
}
```

#### Process Refunds

Processes refunds for affected payments.

**Endpoint:** `POST /api/v1/admin/incidents/payment-failure/refund`

**Authorization:** `AdminOnly` policy

**Request Body:**
```json
{
  "paymentIds": [
    "550e8400-e29b-41d4-a716-446655440000",
    "660e8400-e29b-41d4-a716-446655440001"
  ],
  "reason": "Payment failure incident"
}
```

**Response:** `200 OK`
```json
{
  "refundStatuses": {
    "550e8400-e29b-41d4-a716-446655440000": true,
    "660e8400-e29b-41d4-a716-446655440001": false
  },
  "totalProcessed": 2,
  "successful": 1,
  "failed": 1,
  "errors": ["Payment 2 failed"]
}
```

#### Reset Circuit Breaker

Resets the circuit breaker for a payment provider.

**Endpoint:** `POST /api/v1/admin/incidents/circuit-breaker/reset/{provider}`

**Authorization:** `AdminOnly` policy

**Path Parameters:**
- `provider` - Payment provider name (e.g., "Stripe", "ZainCash")

**Response:** `204 No Content`

#### Get Incident Metrics

Gets incident metrics for a specified time range.

**Endpoint:** `GET /api/v1/admin/incidents/metrics?startDate={startDate}&endDate={endDate}`

**Authorization:** `AdminOnly` policy

**Query Parameters:**
- `startDate` (optional) - Start date for metrics (defaults to 24 hours ago)
- `endDate` (optional) - End date for metrics (defaults to now)

**Response:** `200 OK`
```json
{
  "totalIncidents": 10,
  "criticalIncidents": 2,
  "highSeverityIncidents": 3,
  "mediumSeverityIncidents": 3,
  "lowSeverityIncidents": 2,
  "averageResolutionTime": "00:20:00",
  "incidentsByType": {
    "ProviderError": 5,
    "Timeout": 3,
    "NetworkError": 2
  }
}
```

## Usage Examples

### Assessing a Security Incident

```csharp
var securityEvent = SecurityEvent.Create(
    SecurityEventType.UnauthorizedAccess,
    DateTime.UtcNow,
    userId: "user123",
    ipAddress: "192.168.1.100",
    resource: "/api/admin/payments",
    action: "UnauthorizedAccess",
    succeeded: false,
    details: "Unauthorized access attempt");

var assessment = await _securityIncidentResponseService.AssessIncidentAsync(
    securityEvent,
    cancellationToken);

// Assessment contains:
// - Severity: High
// - ThreatType: UnauthorizedAccess
// - RecommendedContainment: BlockIpAddress
// - RemediationActions: [BlockIpAddress, NotifySecurityTeam]
```

### Containing a Security Incident

```csharp
var incidentId = SecurityIncidentId.FromGuid(guid);
await _securityIncidentResponseService.ContainIncidentAsync(
    incidentId,
    ContainmentStrategy.BlockIpAddress,
    cancellationToken);
```

### Revoking Credentials

```csharp
var request = CredentialRevocationRequest.Create(
    credentialId: "api-key-123",
    credentialType: "ApiKey",
    reason: "Security incident",
    revokedBy: "admin");

await _securityIncidentResponseService.RevokeCredentialsAsync(
    request,
    cancellationToken);
```

### Generating an Incident Report

```csharp
var incidentId = SecurityIncidentId.FromGuid(guid);
var report = await _securityIncidentResponseService.GenerateIncidentReportAsync(
    incidentId,
    cancellationToken);

// Report is a JSON string containing:
// - Incident details
// - Security event information
// - Assessment results
// - Related events
// - Containment timeline
```

## Severity Determination

The service determines incident severity based on:

### Critical Severity
- Data breach events
- Credential compromise events
- More than 50 failed authentication attempts in 24 hours

### High Severity
- Unauthorized access attempts
- Malicious payload detection
- More than 20 failed authentication attempts in 24 hours

### Medium Severity
- Suspicious payment patterns
- Suspicious authentication from unusual locations
- More than 10 failed authentication attempts in 24 hours

### Low Severity
- Rate limit exceeded
- Authentication failures (fewer than 10 in 24 hours)

## Containment Strategy Selection

The service selects containment strategies based on:

### Critical Incidents
- **Credential Attack** → `RevokeCredentials`
- **Denial of Service** → `BlockIpAddress`
- **Other** → `IsolatePod`

### High Severity Incidents
- **With IP Address** → `BlockIpAddress`
- **Other** → `DisableFeature`

### Medium/Low Severity Incidents
- **Default** → `DisableFeature`

## Security Features

### IP Whitelisting

Admin endpoints support IP whitelisting in production:

**Configuration** (`appsettings.json`):
```json
{
  "Security": {
    "IpWhitelist": {
      "Enabled": true,
      "AllowedIps": [
        "10.0.0.1",
        "192.168.1.0/24",
        "172.16.0.0/16"
      ]
    }
  }
}
```

**Features:**
- Only enforced in production when enabled
- Supports exact IP matches and CIDR notation
- Respects `X-Forwarded-For` and `X-Real-IP` headers
- Blocks unauthorized IPs with `403 Forbidden`

### Rate Limiting

Admin endpoints are protected with rate limiting:

**Configuration** (`appsettings.json`):
```json
{
  "IpRateLimiting": {
    "GeneralRules": [
      {
        "Endpoint": "*:/api/v*/admin/*",
        "Period": "1m",
        "Limit": 100
      }
    ]
  }
}
```

**Features:**
- 100 requests per minute per IP for admin endpoints
- Returns `429 Too Many Requests` when limit exceeded
- Tracks rate limit hits in metrics

### Request/Response Logging

All admin endpoint requests and responses are logged:

**Features:**
- Logs request method, path, query string, user ID, IP address
- Logs request body for POST/PUT/PATCH requests
- Logs response status code, elapsed time, response body
- Structured logging with request IDs for correlation
- Logs to structured logger (Serilog) for observability

### Audit Logging

All security operations are logged:

**Features:**
- Security events logged to audit log repository
- Incident assessments logged with severity and threat type
- Containment actions logged with strategy and reason
- Credential revocations logged with credential ID and reason
- All logs include user ID, IP address, and timestamp

## Testing

### Unit Tests

Comprehensive unit tests for `SecurityIncidentResponseService`:

**Location:** `tests/Payment.Application.Tests/Services/SecurityIncidentResponseServiceTests.cs`

**Coverage:**
- Incident assessment with different severity levels
- Containment strategy execution
- Credential revocation (API keys, JWT tokens, bulk)
- Error handling and validation
- Security alert notifications

**Run Tests:**
```bash
dotnet test --filter "FullyQualifiedName~SecurityIncidentResponseServiceTests"
```

### Integration Tests

Integration tests for containment scenarios:

**Location:** `tests/Payment.Application.Tests/Services/SecurityIncidentResponseServiceIntegrationTests.cs`

**Coverage:**
- End-to-end containment workflows
- Credential revocation containment
- IP address blocking
- Pod isolation recommendations
- Complete incident response workflows

**Run Tests:**
```bash
dotnet test --filter "FullyQualifiedName~SecurityIncidentResponseServiceIntegrationTests"
```

### Controller Tests

Integration tests for admin endpoints:

**Locations:**
- `tests/Payment.API.Tests/Controllers/Admin/IncidentControllerTests.cs`
- `tests/Payment.API.Tests/Controllers/Admin/SecurityIncidentControllerTests.cs`

**Coverage:**
- All admin endpoints with success scenarios
- Error handling (404, 400, 403)
- Authorization requirements
- Request/response validation

**Run Tests:**
```bash
dotnet test --filter "FullyQualifiedName~Admin"
```

## Configuration

### Application Settings

**Security Configuration:**
```json
{
  "Security": {
    "IpWhitelist": {
      "Enabled": false,
      "AllowedIps": []
    }
  }
}
```

**Rate Limiting Configuration:**
```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [
      {
        "Endpoint": "*:/api/v*/admin/*",
        "Period": "1m",
        "Limit": 100
      }
    ]
  }
}
```

### Authorization Policies

**AdminOnly Policy:**
```csharp
options.AddPolicy("AdminOnly", policy =>
    policy.RequireClaim("scope", "payment.admin"));
```

**SecurityAdminOnly Policy:**
```csharp
options.AddPolicy("SecurityAdminOnly", policy =>
    policy.RequireClaim("scope", "payment.admin")
          .RequireClaim("role", "SecurityAdmin"));
```

## Best Practices

### Security Incident Response

1. **Immediate Assessment** - Assess all security events immediately upon detection
2. **Severity-Based Response** - Respond based on incident severity (Critical → immediate containment)
3. **Credential Revocation** - Revoke compromised credentials immediately
4. **IP Blocking** - Block malicious IP addresses promptly
5. **Notification** - Notify security team for High/Critical incidents
6. **Documentation** - Generate incident reports for all security incidents
7. **Audit Trail** - Maintain complete audit trail of all security operations

### Admin Endpoint Security

1. **Authorization** - Always use `AdminOnly` or `SecurityAdminOnly` policies
2. **IP Whitelisting** - Enable IP whitelisting in production
3. **Rate Limiting** - Configure appropriate rate limits for admin endpoints
4. **HTTPS Only** - Enforce HTTPS for all admin endpoints
5. **Audit Logging** - Log all admin actions for compliance
6. **Input Validation** - Validate all input to admin endpoints
7. **Error Handling** - Don't expose sensitive information in error messages

## Related Documentation

- [Incident Response Service](./Incident_Response_Service.md) - Payment failure incident response
- [Security Policy](./Security_Policy.md) - Security policies and compliance
- [Authentication Flow](../01-Architecture/Authentication_Flow.md) - Authentication and authorization
- [Observability](../03-Infrastructure/Observability.md) - Monitoring and logging

## See Also

- [System Architecture](../01-Architecture/System_Architecture.md)
- [Kubernetes Deployment](../03-Infrastructure/Kubernetes_Deployment.md)
- [Testing Strategy](../04-Guidelines/Testing_Strategy.md)

