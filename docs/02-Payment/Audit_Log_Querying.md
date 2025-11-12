---
title: Audit Log Querying Tools
version: 1.0
last_updated: 2025-01-15
category: Payment
tags:
  - audit-logging
  - compliance
  - security
  - querying
  - reporting
  - retention-policy
summary: >
  Comprehensive audit log querying system with advanced filtering, pagination, sorting,
  export capabilities, and automated retention policy management for compliance and security.
related_docs:
  - Security_Policy.md
  - Security_Incident_Response_Service.md
  - Incident_Response_Service.md
  - ../03-Infrastructure/Observability.md
ai_context_priority: high
---

# 📋 Audit Log Querying Tools

The Payment Microservice includes a comprehensive **Audit Log Querying System** that provides advanced querying, filtering, export capabilities, and automated retention policy management. The system enables security administrators to query audit logs with sophisticated filtering, generate summaries, export data, and automatically manage log retention for compliance.

## Overview

The Audit Log Querying System is designed to provide:

1. **Advanced Querying** - Filter audit logs by user, IP address, event type, resource, time range, and full-text search
2. **Pagination & Sorting** - Efficient pagination with customizable sorting
3. **Summary Statistics** - Aggregate statistics for time ranges
4. **Security Event Detection** - Specialized queries for security-related events
5. **Export Capabilities** - Export audit logs to CSV and JSON formats
6. **Retention Policy** - Automated background service for log retention (90 days hot, 1 year cold)
7. **Performance Optimization** - Database indexes for fast query execution

## Features

- ✅ **Advanced Filtering** - Filter by UserId, IpAddress, EventType, Resource, EntityId, TimeRange
- ✅ **Full-Text Search** - Search across action, entity type, and user ID fields
- ✅ **Pagination** - Efficient pagination with configurable page size (max 1000)
- ✅ **Sorting** - Sort by Timestamp, UserId, Action, or EntityType (ascending/descending)
- ✅ **Summary Statistics** - Aggregate statistics including total events, unique users, top users, top IPs
- ✅ **Security Event Queries** - Specialized queries for security-related events
- ✅ **CSV Export** - Export audit logs to CSV format
- ✅ **JSON Export** - Export audit logs to JSON format with metadata
- ✅ **Retention Policy** - Automated background service (90 days hot, 1 year cold)
- ✅ **Database Indexes** - Optimized indexes for fast query performance
- ✅ **Security Admin Only** - All endpoints require `SecurityAdminOnly` authorization policy
- ✅ **Stateless Design** - Suitable for Kubernetes horizontal scaling

## Architecture

### Components

1. **IAuditLogQueryService** (`Payment.Application.Interfaces.IAuditLogQueryService`)
   - Main interface for audit log querying operations
   - Located in Application layer following Clean Architecture

2. **AuditLogQueryService** (`Payment.Infrastructure.Auditing.AuditLogQueryService`)
   - Core implementation of audit log querying logic
   - Handles filtering, pagination, sorting, and export

3. **AuditLogController** (`Payment.API.Controllers.Admin.AuditLogController`)
   - REST API controller for audit log queries
   - Requires `SecurityAdminOnly` authorization policy

4. **AuditLogRetentionService** (`Payment.Infrastructure.BackgroundServices.AuditLogRetentionService`)
   - Background service for automated log retention
   - Runs daily to archive and delete old logs

5. **IAuditLogRepository** (`Payment.Domain.Interfaces.IAuditLogRepository`)
   - Repository interface for audit log persistence
   - Implemented in Infrastructure layer

## Data Models

### AuditLogQuery

Query parameters for filtering and pagination:

```csharp
public sealed record AuditLogQuery
{
    public string? UserId { get; init; }
    public string? IpAddress { get; init; }
    public string? EventType { get; init; }
    public string? Resource { get; init; }
    public Guid? EntityId { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public string? SearchQuery { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? SortBy { get; init; } = "Timestamp";
    public string? SortDirection { get; init; } = "desc";
}
```

**Properties:**
- `UserId` - Filter by user ID
- `IpAddress` - Filter by IP address
- `EventType` - Filter by event type/action (e.g., "PaymentCreated", "PaymentRefunded")
- `Resource` - Filter by resource/entity type (e.g., "Payment", "Refund")
- `EntityId` - Filter by specific entity ID
- `StartTime` - Start time for time range filter (UTC)
- `EndTime` - End time for time range filter (UTC)
- `SearchQuery` - Full-text search query
- `Page` - Page number (1-based)
- `PageSize` - Page size (1-1000, default: 50)
- `SortBy` - Sort field (default: "Timestamp")
- `SortDirection` - Sort direction ("asc" or "desc", default: "desc")

### AuditLogQueryResult

Paginated query results:

```csharp
public sealed record AuditLogQueryResult
{
    public IEnumerable<AuditLogEntryDto> Entries { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; }
    public bool HasPreviousPage { get; }
    public bool HasNextPage { get; }
}
```

### AuditLogSummary

Summary statistics for a time range:

```csharp
public sealed record AuditLogSummary
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int TotalEvents { get; init; }
    public int UniqueUsers { get; init; }
    public int UniqueIpAddresses { get; init; }
    public Dictionary<string, int> EventsByType { get; init; }
    public Dictionary<string, int> EventsByResource { get; init; }
    public Dictionary<string, int> TopUsers { get; init; }
    public Dictionary<string, int> TopIpAddresses { get; init; }
}
```

### SecurityEventQuery

Query parameters for security events:

```csharp
public sealed record SecurityEventQuery
{
    public string? UserId { get; init; }
    public string? IpAddress { get; init; }
    public string? EventType { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
```

### SecurityEventDto

Security event representation:

```csharp
public sealed record SecurityEventDto
{
    public Guid Id { get; init; }
    public string UserId { get; init; }
    public string EventType { get; init; }
    public string Severity { get; init; } // Critical, High, Medium, Low
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, object> Metadata { get; init; }
    public DateTime Timestamp { get; init; }
}
```

## API Endpoints

All endpoints require `SecurityAdminOnly` authorization policy (requires `payment.admin` scope and `SecurityAdmin` role).

### Query Audit Logs

Query audit logs with filtering, pagination, and sorting.

**Endpoint:** `GET /api/v1/admin/audit-logs`

**Query Parameters:**
- `userId` (string, optional) - Filter by user ID
- `ipAddress` (string, optional) - Filter by IP address
- `eventType` (string, optional) - Filter by event type
- `resource` (string, optional) - Filter by resource type
- `entityId` (Guid, optional) - Filter by entity ID
- `startTime` (DateTime, optional) - Start time for time range
- `endTime` (DateTime, optional) - End time for time range
- `searchQuery` (string, optional) - Full-text search query
- `page` (int, default: 1) - Page number
- `pageSize` (int, default: 50, max: 1000) - Page size
- `sortBy` (string, default: "Timestamp") - Sort field
- `sortDirection` (string, default: "desc") - Sort direction

**Example Request:**
```http
GET /api/v1/admin/audit-logs?userId=user123&startTime=2025-01-01T00:00:00Z&endTime=2025-01-31T23:59:59Z&page=1&pageSize=50
Authorization: Bearer <token>
```

**Example Response:**
```json
{
  "entries": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "userId": "user123",
      "action": "PaymentCreated",
      "entityType": "Payment",
      "entityId": "660e8400-e29b-41d4-a716-446655440001",
      "ipAddress": "192.168.1.1",
      "userAgent": "Mozilla/5.0",
      "changes": {
        "Amount": 100.00,
        "Currency": "USD"
      },
      "timestamp": "2025-01-15T10:30:00Z"
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### Get Audit Log Summary

Get summary statistics for a time range.

**Endpoint:** `GET /api/v1/admin/audit-logs/summary`

**Query Parameters:**
- `startTime` (DateTime, required) - Start time for summary
- `endTime` (DateTime, required) - End time for summary

**Example Request:**
```http
GET /api/v1/admin/audit-logs/summary?startTime=2025-01-01T00:00:00Z&endTime=2025-01-31T23:59:59Z
Authorization: Bearer <token>
```

**Example Response:**
```json
{
  "startTime": "2025-01-01T00:00:00Z",
  "endTime": "2025-01-31T23:59:59Z",
  "totalEvents": 1500,
  "uniqueUsers": 250,
  "uniqueIpAddresses": 45,
  "eventsByType": {
    "PaymentCreated": 800,
    "PaymentRefunded": 200,
    "PaymentCompleted": 500
  },
  "eventsByResource": {
    "Payment": 1500
  },
  "topUsers": {
    "user123": 150,
    "user456": 120,
    "user789": 100
  },
  "topIpAddresses": {
    "192.168.1.1": 200,
    "192.168.1.2": 150,
    "10.0.0.1": 100
  }
}
```

### Get Security Events

Query security-related events.

**Endpoint:** `GET /api/v1/admin/audit-logs/security-events`

**Query Parameters:**
- `userId` (string, optional) - Filter by user ID
- `ipAddress` (string, optional) - Filter by IP address
- `eventType` (string, optional) - Filter by event type
- `startTime` (DateTime, optional) - Start time for time range
- `endTime` (DateTime, optional) - End time for time range
- `page` (int, default: 1) - Page number
- `pageSize` (int, default: 50) - Page size

**Example Request:**
```http
GET /api/v1/admin/audit-logs/security-events?startTime=2025-01-01T00:00:00Z&severity=High
Authorization: Bearer <token>
```

**Example Response:**
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "userId": "user123",
    "eventType": "UnauthorizedAccess",
    "severity": "High",
    "ipAddress": "10.0.0.1",
    "userAgent": "Unknown",
    "description": "Security event: UnauthorizedAccess on Payment",
    "metadata": {
      "Reason": "Invalid token"
    },
    "timestamp": "2025-01-15T10:30:00Z"
  }
]
```

### Export to CSV

Export audit logs to CSV format.

**Endpoint:** `GET /api/v1/admin/audit-logs/export/csv`

**Query Parameters:** Same as query endpoint (all filtering options supported)

**Example Request:**
```http
GET /api/v1/admin/audit-logs/export/csv?startTime=2025-01-01T00:00:00Z&endTime=2025-01-31T23:59:59Z
Authorization: Bearer <token>
```

**Response:** CSV file download

### Export to JSON

Export audit logs to JSON format.

**Endpoint:** `GET /api/v1/admin/audit-logs/export/json`

**Query Parameters:** Same as query endpoint (all filtering options supported)

**Example Request:**
```http
GET /api/v1/admin/audit-logs/export/json?startTime=2025-01-01T00:00:00Z&endTime=2025-01-31T23:59:59Z
Authorization: Bearer <token>
```

**Response:** JSON file download with metadata

## Retention Policy

The system includes an automated background service (`AuditLogRetentionService`) that manages audit log retention:

- **Hot Storage (0-90 days)**: Logs remain in the main `AuditLogs` table for fast access
- **Cold Storage (90-365 days)**: Logs older than 90 days can be archived to separate storage
- **Deletion (365+ days)**: Logs older than 1 year are automatically deleted

The retention service runs daily and:
1. Archives logs older than 90 days (moves to cold storage)
2. Deletes logs older than 365 days

**Configuration:**
- Cleanup interval: Daily
- Hot retention period: 90 days
- Cold retention period: 365 days total

## Database Indexes

The system includes optimized database indexes for fast query performance:

- `IX_AuditLogs_UserId` - Index on UserId
- `IX_AuditLogs_IpAddress` - Index on IpAddress
- `IX_AuditLogs_Action` - Index on Action (EventType)
- `IX_AuditLogs_Timestamp` - Index on Timestamp
- `IX_AuditLogs_Timestamp_UserId` - Composite index for time range queries with user filtering
- `IX_AuditLogs_Timestamp_IpAddress` - Composite index for time range queries with IP filtering

**Migration:**
```bash
dotnet ef migrations add AddAuditLogIndexes --project src/Payment.Infrastructure --startup-project src/Payment.API
dotnet ef database update --project src/Payment.Infrastructure --startup-project src/Payment.API
```

## Security Event Detection

The system automatically detects security-related events based on event type patterns:

- `UnauthorizedAccess`
- `FailedAuthentication`
- `SuspiciousActivity`
- `RateLimitExceeded`
- `InvalidToken`
- `CredentialRevocation`
- `SecurityPolicyViolation`

Events matching these patterns are automatically classified with severity levels:
- **Critical**: Unauthorized access, security breaches
- **High**: Failed authentication, suspicious activity
- **Medium**: Rate limit exceeded
- **Low**: Other security events

## Usage Examples

### Query Recent Audit Logs

```csharp
var query = new AuditLogQuery
{
    StartTime = DateTime.UtcNow.AddDays(-7),
    EndTime = DateTime.UtcNow,
    Page = 1,
    PageSize = 50,
    SortBy = "Timestamp",
    SortDirection = "desc"
};

var result = await _auditLogQueryService.QueryAsync(query);
```

### Search for Specific User Activity

```csharp
var query = new AuditLogQuery
{
    UserId = "user123",
    StartTime = DateTime.UtcNow.AddDays(-30),
    EndTime = DateTime.UtcNow,
    Page = 1,
    PageSize = 100
};

var result = await _auditLogQueryService.QueryAsync(query);
```

### Get Security Events for IP Address

```csharp
var query = new SecurityEventQuery
{
    IpAddress = "10.0.0.1",
    StartTime = DateTime.UtcNow.AddDays(-7),
    EndTime = DateTime.UtcNow,
    Page = 1,
    PageSize = 50
};

var events = await _auditLogQueryService.GetSecurityEventsAsync(query);
```

### Export Audit Logs for Compliance

```csharp
var query = new AuditLogQuery
{
    StartTime = new DateTime(2025, 1, 1),
    EndTime = new DateTime(2025, 1, 31),
    PageSize = 10000 // Large page size for export
};

var csvData = await _auditLogQueryService.ExportToCsvAsync(query);
// Save csvData to file or send to compliance team
```

## Testing

Comprehensive unit tests are available in `tests/Payment.Infrastructure.Tests/Auditing/AuditLogQueryServiceTests.cs`:

- Querying with no filters
- Filtering by UserId, IpAddress, EventType
- Time range filtering
- Pagination
- Sorting (ascending/descending)
- Full-text search
- Summary statistics
- Security events querying
- CSV export
- JSON export

## Related Documentation

- [Security Policy](./Security_Policy.md) - Security policies and compliance requirements
- [Security Incident Response Service](./Security_Incident_Response_Service.md) - Security incident handling
- [Incident Response Service](./Incident_Response_Service.md) - Payment failure incident handling
- [Observability](../03-Infrastructure/Observability.md) - Monitoring and observability

## Best Practices

1. **Use Pagination**: Always use pagination for large result sets to avoid performance issues
2. **Filter Early**: Apply time range filters to reduce query scope
3. **Index Usage**: Queries automatically use optimized indexes for fast execution
4. **Export for Compliance**: Use export functionality for compliance reporting
5. **Monitor Retention**: Review retention service logs to ensure proper log management
6. **Security Events**: Regularly query security events to identify potential threats

