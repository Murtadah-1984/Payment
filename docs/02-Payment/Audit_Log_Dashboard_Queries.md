# Audit Log Dashboard Queries

This document provides pre-built queries for monitoring and analyzing audit logs in Grafana, CloudWatch, and other monitoring dashboards.

## Overview

Audit logs are critical for security monitoring, compliance, and incident response. These queries help you:

- Monitor security events in real-time
- Identify suspicious patterns
- Track user activity
- Generate compliance reports
- Investigate security incidents

## Grafana Queries

### Security Events Over Time

```promql
# Count of security events by type (last 24 hours)
sum by (event_type) (
  rate(audit_log_events_total{event_type=~"Security.*"}[5m])
) * 3600
```

### Failed Authentication Attempts

```promql
# Failed authentication attempts per hour
sum(rate(audit_log_events_total{event_type="AuthenticationFailure",succeeded="false"}[1h])) * 3600
```

### Unauthorized Access Attempts

```promql
# Unauthorized access attempts by IP address
sum by (ip_address) (
  rate(audit_log_events_total{event_type="UnauthorizedAccess"}[5m])
) * 3600
```

### Top Users by Activity

```promql
# Top 10 users by audit log events
topk(10, sum by (user_id) (
  rate(audit_log_events_total[1h])
))
```

### Suspicious IP Addresses

```promql
# IP addresses with high failure rate
sum by (ip_address) (
  rate(audit_log_events_total{succeeded="false"}[5m])
) / sum by (ip_address) (
  rate(audit_log_events_total[5m])
) > 0.5
```

### Audit Log Volume

```promql
# Total audit log events per hour
sum(rate(audit_log_events_total[1h])) * 3600
```

## CloudWatch Insights Queries

### Security Events Summary

```sql
fields @timestamp, eventType, userId, ipAddress, resource, action, succeeded
| filter eventType like /Security|Unauthorized|Authentication/
| stats count() by eventType, bin(5m)
| sort @timestamp desc
```

### Failed Authentication Analysis

```sql
fields @timestamp, userId, ipAddress, resource
| filter eventType = "AuthenticationFailure" and succeeded = false
| stats count() by userId, ipAddress, bin(1h)
| sort count desc
```

### Unauthorized Access Patterns

```sql
fields @timestamp, userId, ipAddress, resource, action
| filter eventType = "UnauthorizedAccess"
| stats count() by ipAddress, resource, bin(15m)
| sort count desc
```

### User Activity Timeline

```sql
fields @timestamp, userId, eventType, resource, action
| filter userId = "user-123"
| sort @timestamp desc
| limit 100
```

### IP Address Reputation Check

```sql
fields @timestamp, ipAddress, eventType, succeeded
| filter ipAddress = "192.168.1.100"
| stats count() as totalEvents, 
        count(succeeded = false) as failedEvents,
        count(succeeded = true) as successfulEvents
| sort @timestamp desc
```

### High-Risk Events

```sql
fields @timestamp, eventType, userId, ipAddress, resource, details
| filter eventType in ["CredentialCompromise", "DataBreach", "MaliciousPayload"]
| sort @timestamp desc
```

## PostgreSQL Queries

### Recent Security Events

```sql
SELECT 
    "EventType",
    "UserId",
    "IpAddress",
    "Resource",
    "Action",
    "Succeeded",
    "Timestamp"
FROM "AuditLogs"
WHERE "EventType" LIKE '%Security%'
    AND "Timestamp" >= NOW() - INTERVAL '24 hours'
ORDER BY "Timestamp" DESC
LIMIT 100;
```

### Failed Authentication Attempts by IP

```sql
SELECT 
    "IpAddress",
    COUNT(*) as failure_count,
    COUNT(DISTINCT "UserId") as unique_users,
    MIN("Timestamp") as first_attempt,
    MAX("Timestamp") as last_attempt
FROM "AuditLogs"
WHERE "EventType" = 'AuthenticationFailure'
    AND "Succeeded" = false
    AND "Timestamp" >= NOW() - INTERVAL '1 hour'
GROUP BY "IpAddress"
HAVING COUNT(*) > 5
ORDER BY failure_count DESC;
```

### User Activity Summary

```sql
SELECT 
    "UserId",
    COUNT(*) as total_events,
    COUNT(DISTINCT "Resource") as unique_resources,
    COUNT(CASE WHEN "Succeeded" = false THEN 1 END) as failed_events,
    MIN("Timestamp") as first_event,
    MAX("Timestamp") as last_event
FROM "AuditLogs"
WHERE "UserId" IS NOT NULL
    AND "Timestamp" >= NOW() - INTERVAL '24 hours'
GROUP BY "UserId"
ORDER BY total_events DESC
LIMIT 20;
```

### Resource Access Patterns

```sql
SELECT 
    "Resource",
    "Action",
    COUNT(*) as access_count,
    COUNT(DISTINCT "UserId") as unique_users,
    COUNT(CASE WHEN "Succeeded" = false THEN 1 END) as failed_count
FROM "AuditLogs"
WHERE "Timestamp" >= NOW() - INTERVAL '24 hours'
GROUP BY "Resource", "Action"
ORDER BY access_count DESC
LIMIT 50;
```

### Security Incident Detection

```sql
SELECT 
    "EventType",
    "UserId",
    "IpAddress",
    "Resource",
    "Timestamp",
    "Details"
FROM "AuditLogs"
WHERE "EventType" IN (
    'UnauthorizedAccess',
    'CredentialCompromise',
    'DataBreach',
    'MaliciousPayload'
)
    AND "Timestamp" >= NOW() - INTERVAL '1 hour'
ORDER BY "Timestamp" DESC;
```

## Dashboard Panels

### Recommended Grafana Dashboard Panels

1. **Security Events Over Time** (Time Series)
   - Query: Security events by type
   - Time range: Last 24 hours
   - Refresh: 1 minute

2. **Failed Authentication Rate** (Stat)
   - Query: Failed authentication attempts
   - Thresholds: < 10 (green), 10-50 (yellow), > 50 (red)

3. **Top Suspicious IPs** (Table)
   - Query: IPs with high failure rate
   - Sort: By failure count descending

4. **User Activity Heatmap** (Heatmap)
   - Query: Events by user and hour
   - Time range: Last 7 days

5. **Security Event Types** (Pie Chart)
   - Query: Count by event type
   - Time range: Last 24 hours

6. **Audit Log Volume** (Graph)
   - Query: Total events per hour
   - Time range: Last 7 days

## Alert Rules

### High Failed Authentication Rate

```yaml
alert: HighFailedAuthenticationRate
expr: |
  sum(rate(audit_log_events_total{event_type="AuthenticationFailure",succeeded="false"}[5m])) * 3600 > 50
for: 5m
labels:
  severity: warning
annotations:
  summary: "High failed authentication rate detected"
  description: "{{ $value }} failed authentication attempts in the last hour"
```

### Unauthorized Access Detected

```yaml
alert: UnauthorizedAccessDetected
expr: |
  increase(audit_log_events_total{event_type="UnauthorizedAccess"}[5m]) > 0
for: 1m
labels:
  severity: critical
annotations:
  summary: "Unauthorized access attempt detected"
  description: "Unauthorized access attempt from IP {{ $labels.ip_address }}"
```

### Suspicious IP Activity

```yaml
alert: SuspiciousIPActivity
expr: |
  sum by (ip_address) (
    rate(audit_log_events_total{succeeded="false"}[5m])
  ) / sum by (ip_address) (
    rate(audit_log_events_total[5m])
  ) > 0.8
for: 10m
labels:
  severity: warning
annotations:
  summary: "Suspicious activity from IP {{ $labels.ip_address }}"
  description: "{{ $value | humanizePercentage }} failure rate from this IP"
```

## Export and Reporting

### Export Audit Logs for Compliance

Use the audit log query service API:

```bash
# Export last 30 days of audit logs
curl -X GET "https://api.example.com/api/v1/admin/audit-logs/export?format=csv&startTime=2024-01-01&endTime=2024-01-31" \
  -H "Authorization: Bearer $TOKEN" \
  -o audit-logs-2024-01.csv
```

### Generate Security Report

```bash
# Generate security incident report
curl -X POST "https://api.example.com/api/v1/admin/incidents/security/report" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "incidentId": "incident-123",
    "format": "pdf"
  }' \
  -o security-report.pdf
```

## Best Practices

1. **Retention Policy**: Keep hot data (last 90 days) in primary database, archive older data
2. **Indexing**: Ensure indexes on `Timestamp`, `UserId`, `IpAddress`, `EventType`
3. **Monitoring**: Set up alerts for critical security events
4. **Regular Reviews**: Review audit logs weekly for anomalies
5. **Compliance**: Export logs regularly for compliance requirements

## Related Documentation

- [Audit Log Querying](./Audit_Log_Querying.md)
- [Security Policy](./Security_Policy.md)
- [Incident Response Service](./Incident_Response_Service.md)

