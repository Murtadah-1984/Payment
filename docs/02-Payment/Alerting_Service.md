---
title: Automated Alerting Service
version: 1.0
last_updated: 2025-01-15
category: Payment
tags:
  - alerting
  - monitoring
  - notifications
  - prometheus
  - metrics
  - email
  - slack
  - pagerduty
  - sms
summary: >
  Comprehensive automated alerting system for critical incidents with multi-channel notifications,
  alert deduplication, severity-based routing, and Prometheus metrics integration.
related_docs:
  - Incident_Response_Service.md
  - Security_Incident_Response_Service.md
  - ../03-Infrastructure/Observability.md
  - ../03-Infrastructure/Kubernetes_Deployment.md
  - ../01-Architecture/System_Architecture.md
ai_context_priority: high
---

# 🚨 Automated Alerting Service

The Payment Microservice includes a comprehensive **Automated Alerting Service** that provides multi-channel notifications for critical incidents, payment failures, and security events. The service features alert deduplication, severity-based routing, Prometheus metrics integration, and alert acknowledgment capabilities.

## Overview

The Alerting Service is designed to ensure timely notification of critical incidents through multiple channels:

1. **Multi-Channel Notifications** - Email, Slack, PagerDuty, and SMS support
2. **Alert Deduplication** - Prevents alert storms by deduplicating similar alerts
3. **Severity-Based Routing** - Routes alerts to appropriate channels based on severity
4. **Prometheus Metrics** - Comprehensive metrics for monitoring alert performance
5. **Alert Acknowledgment** - Track which alerts have been acknowledged by operators
6. **Configurable Rules** - Flexible alert rules configuration via appsettings.json

## Features

- ✅ **Multi-Channel Support** - Email (SMTP), Slack webhooks, PagerDuty, and SMS (Twilio/AWS SNS)
- ✅ **Alert Deduplication** - Prevents duplicate alerts within a configurable time window
- ✅ **Severity-Based Routing** - Routes alerts to channels based on severity (Low, Medium, High, Critical)
- ✅ **Prometheus Metrics** - Tracks alert counts, deduplications, channel failures, and sending duration
- ✅ **Alert Acknowledgment** - Operators can acknowledge alerts to track response
- ✅ **HTML Email Templates** - Rich HTML email templates with severity color coding
- ✅ **Configurable Alert Rules** - JSON-based configuration for alert thresholds and channels
- ✅ **Stateless Design** - Suitable for Kubernetes horizontal scaling
- ✅ **Comprehensive Logging** - Structured logging for observability

## Architecture

### Components

1. **IAlertingService** (`Payment.Domain.Interfaces.IAlertingService`)
   - Main interface for alerting operations
   - Located in Domain layer following Clean Architecture

2. **AlertingService** (`Payment.Infrastructure.Monitoring.AlertingService`)
   - Core implementation of alerting logic
   - Handles deduplication, routing, and channel selection

3. **IAlertChannel** (`Payment.Infrastructure.Monitoring.IAlertChannel`)
   - Interface for alert notification channels
   - Each channel implements this interface

4. **Alert Channels**
   - `EmailAlertChannel` - SMTP email notifications
   - `SlackAlertChannel` - Slack webhook integration
   - `PagerDutyAlertChannel` - PagerDuty integration (Critical only)
   - `SmsAlertChannel` - SMS via Twilio or AWS SNS

5. **AlertMetrics** (`Payment.Infrastructure.Metrics.AlertMetrics`)
   - Prometheus metrics for alerting system
   - Tracks sent alerts, deduplications, failures, and duration

6. **AlertRulesConfiguration** (`Payment.Infrastructure.Monitoring.AlertRulesConfiguration`)
   - Configuration model for alert rules
   - Supports payment failure and security incident rules

### Data Models

#### AlertSeverity

```csharp
public enum AlertSeverity
{
    Low = 0,      // Informational alerts
    Medium = 1,   // Warnings requiring attention
    High = 2,     // Significant issues requiring immediate attention
    Critical = 3  // Severe issues requiring immediate escalation
}
```

#### AlertAcknowledgment

```csharp
public class AlertAcknowledgment
{
    public string AlertKey { get; set; }
    public string AcknowledgedBy { get; set; }
    public DateTime AcknowledgedAt { get; set; }
    public string? Notes { get; set; }
}
```

## Usage

### Sending Generic Alerts

```csharp
public class MyService
{
    private readonly IAlertingService _alertingService;

    public MyService(IAlertingService alertingService)
    {
        _alertingService = alertingService;
    }

    public async Task HandleCriticalIssue()
    {
        await _alertingService.SendAlertAsync(
            severity: AlertSeverity.Critical,
            title: "Database Connection Failure",
            message: "Unable to connect to primary database",
            metadata: new Dictionary<string, object>
            {
                ["Database"] = "PaymentDb",
                ["Error"] = "Timeout after 30 seconds",
                ["RetryCount"] = 3
            },
            ct: CancellationToken.None);
    }
}
```

### Sending Payment Failure Alerts

```csharp
var context = new PaymentFailureContext(
    StartTime: DateTime.UtcNow,
    EndTime: null,
    Provider: "Stripe",
    FailureType: PaymentFailureType.ProviderError,
    AffectedPaymentCount: 15,
    Metadata: new Dictionary<string, object>());

await _alertingService.SendPaymentFailureAlertAsync(context);
```

### Sending Security Incident Alerts

```csharp
var securityEvent = SecurityEvent.Create(
    SecurityEventType.UnauthorizedAccess,
    DateTime.UtcNow,
    userId: "user123",
    ipAddress: "192.168.1.1",
    resource: "/api/payments",
    action: "GET",
    succeeded: false,
    details: "Unauthorized access attempt");

await _alertingService.SendSecurityIncidentAlertAsync(securityEvent);
```

### Acknowledging Alerts

```csharp
// Acknowledge an alert
await _alertingService.AcknowledgeAlertAsync(
    alertKey: "base64-encoded-alert-key",
    acknowledgedBy: "operator@example.com",
    notes: "Investigating the issue",
    ct: CancellationToken.None);

// Check if alert is acknowledged
var isAcknowledged = await _alertingService.IsAlertAcknowledgedAsync(
    alertKey: "base64-encoded-alert-key",
    ct: CancellationToken.None);
```

## Configuration

### Alert Rules Configuration

Configure alert rules in `appsettings.json`:

```json
{
  "AlertRules": {
    "PaymentFailure": {
      "Critical": {
        "Threshold": "> 10 failures in 5 minutes",
        "Channels": ["PagerDuty", "Email", "Slack"]
      },
      "High": {
        "Threshold": "> 5 failures in 5 minutes",
        "Channels": ["Email", "Slack"]
      },
      "Medium": {
        "Threshold": "> 2 failures in 5 minutes",
        "Channels": ["Email"]
      },
      "Low": {
        "Threshold": "> 0 failures in 5 minutes",
        "Channels": ["Email"]
      }
    },
    "SecurityIncident": {
      "Critical": {
        "Threshold": "Any unauthorized access",
        "Channels": ["PagerDuty", "Email", "SMS"]
      },
      "High": {
        "Threshold": "Malicious payload detected",
        "Channels": ["Email", "Slack"]
      },
      "Medium": {
        "Threshold": "Suspicious authentication",
        "Channels": ["Email"]
      },
      "Low": {
        "Threshold": "Rate limit exceeded",
        "Channels": ["Email"]
      }
    }
  }
}
```

### Email Channel Configuration

```json
{
  "Alerting": {
    "Email": {
      "SmtpHost": "smtp.gmail.com",
      "SmtpPort": 587,
      "EnableSsl": true,
      "Username": "alerts@yourdomain.com",
      "Password": "your-app-password",
      "FromAddress": "alerts@yourdomain.com",
      "ToAddresses": [
        "admin@yourdomain.com",
        "ops@yourdomain.com"
      ]
    }
  }
}
```

### Slack Channel Configuration

```json
{
  "Alerting": {
    "Slack": {
      "WebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
      "Channel": "#alerts",
      "Username": "Payment Service"
    }
  }
}
```

### PagerDuty Channel Configuration

```json
{
  "Alerting": {
    "PagerDuty": {
      "IntegrationKey": "your-pagerduty-integration-key"
    }
  }
}
```

### SMS Channel Configuration

**Twilio:**
```json
{
  "Alerting": {
    "SMS": {
      "Provider": "Twilio",
      "PhoneNumbers": ["+1234567890"],
      "TwilioAccountSid": "your-account-sid",
      "TwilioAuthToken": "your-auth-token",
      "TwilioFromNumber": "+1234567890"
    }
  }
}
```

**AWS SNS:**
```json
{
  "Alerting": {
    "SMS": {
      "Provider": "AWSSNS",
      "PhoneNumbers": ["+1234567890"],
      "AwsSnsTopicArn": "arn:aws:sns:us-east-1:123456789012:alerts",
      "AwsRegion": "us-east-1"
    }
  }
}
```

## Alert Deduplication

The alerting service implements deduplication to prevent alert storms:

- **Time Window**: 5 minutes (configurable)
- **Storage**: Distributed cache (Redis) with fallback to in-memory cache
- **Key Generation**: Base64-encoded hash of severity, title, and message
- **Metrics**: Deduplicated alerts are tracked in Prometheus metrics

### How It Works

1. When an alert is sent, a unique key is generated from severity, title, and message
2. The service checks if an alert with the same key was sent within the deduplication window
3. If found, the alert is deduplicated and not sent again
4. If not found, the alert is sent and the key is cached for the deduplication window

## Severity-Based Routing

Alerts are routed to channels based on their severity and the alert rules configuration:

- **Critical**: PagerDuty, Email, Slack, SMS
- **High**: Email, Slack, SMS
- **Medium**: Email, Slack
- **Low**: Email

Each channel has a `MinimumSeverity` property that determines the minimum severity level it accepts.

## Prometheus Metrics

The alerting service exposes the following Prometheus metrics:

### Metrics

1. **`payment_alerts_sent_total`** (Counter)
   - Labels: `severity`, `channel`, `type`
   - Description: Total number of alerts sent

2. **`payment_alerts_deduplicated_total`** (Counter)
   - Labels: `severity`, `type`
   - Description: Total number of alerts that were deduplicated

3. **`payment_alert_channel_failures_total`** (Counter)
   - Labels: `channel`, `severity`
   - Description: Total number of alert channel failures

4. **`payment_alert_sending_duration_seconds`** (Histogram)
   - Labels: `channel`, `severity`
   - Description: Duration of alert sending in seconds
   - Buckets: 0.1, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0

### Example Queries

```promql
# Alert rate by severity
rate(payment_alerts_sent_total[5m])

# Channel failure rate
rate(payment_alert_channel_failures_total[5m])

# Average alert sending duration
histogram_quantile(0.95, payment_alert_sending_duration_seconds)

# Deduplication rate
rate(payment_alerts_deduplicated_total[5m])
```

## Email Templates

The email channel uses HTML templates with severity-based color coding:

- **Critical**: Red (#dc3545)
- **High**: Orange (#fd7e14)
- **Medium**: Yellow (#ffc107)
- **Low**: Cyan (#0dcaf0)

Templates include:
- Severity badge with color coding
- Alert title and message
- Metadata table (if provided)
- Timestamp

## Integration with Incident Response

The alerting service integrates with the Incident Response Service:

```csharp
// In IncidentResponseService
await _alertingService.SendPaymentFailureAlertAsync(
    paymentFailureContext,
    cancellationToken);
```

## Error Handling

- **Channel Failures**: If a channel fails, the service logs the error and continues with other channels
- **Cache Failures**: Falls back to in-memory cache if distributed cache is unavailable
- **Metrics Failures**: Metrics recording failures are logged but don't block alert sending

## Testing

Unit tests are available in `tests/Payment.Infrastructure.Tests/Monitoring/AlertingServiceTests.cs`:

- Alert sending
- Alert deduplication
- Payment failure alerts
- Security incident alerts
- Channel failure handling
- Metrics recording

## Best Practices

1. **Use Appropriate Severity**: Choose the correct severity level to ensure alerts reach the right channels
2. **Include Metadata**: Provide relevant metadata to help operators understand the alert context
3. **Monitor Metrics**: Set up Grafana dashboards to monitor alert rates and channel health
4. **Configure Deduplication**: Adjust deduplication window based on your alert patterns
5. **Test Channels**: Verify all channels are working before deploying to production
6. **Acknowledge Alerts**: Use acknowledgment to track which alerts have been handled

## Troubleshooting

### Alerts Not Being Sent

1. Check channel configuration in `appsettings.json`
2. Verify credentials (SMTP, Slack webhook, PagerDuty key, etc.)
3. Check logs for channel-specific errors
4. Verify alert rules configuration matches severity levels

### High Deduplication Rate

1. Review alert deduplication window
2. Check if similar alerts are being sent too frequently
3. Consider adjusting deduplication window if needed

### Channel Failures

1. Check network connectivity
2. Verify credentials are correct
3. Check channel-specific logs
4. Review Prometheus metrics for failure patterns

## Related Documentation

- [Incident Response Service](./Incident_Response_Service.md) - Payment failure incident handling
- [Security Incident Response Service](./Security_Incident_Response_Service.md) - Security incident handling
- [Observability](../03-Infrastructure/Observability.md) - Monitoring and metrics
- [Kubernetes Deployment](../03-Infrastructure/Kubernetes_Deployment.md) - Deployment configuration

