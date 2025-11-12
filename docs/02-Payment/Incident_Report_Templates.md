---
title: Incident Report Templates
version: 1.0
last_updated: 2025-01-15
category: Payment
tags:
  - incident-reporting
  - templates
  - pdf-generation
  - compliance
  - documentation
summary: >
  Comprehensive incident report generation system with customizable templates,
  multiple export formats (Markdown, HTML, PDF), and support for payment failure
  and security incident reports.
related_docs:
  - Incident_Response_Service.md
  - Security_Incident_Response_Service.md
  - Audit_Log_Querying.md
  - ../runbooks/README.md
ai_context_priority: high
---

# 📄 Incident Report Templates

The Payment Microservice includes a comprehensive **Incident Report Generation System** that creates detailed, professional incident reports in multiple formats. The system supports customizable report sections, multiple export formats (Markdown, HTML, PDF), and generates reports for both payment failure and security incidents.

## Overview

The Incident Report Generation System provides:

1. **Template-Based Generation** - Structured templates for consistent report formatting
2. **Multiple Formats** - Export to Markdown, HTML, and PDF formats
3. **Customizable Sections** - Include/exclude report sections based on requirements
4. **Payment Failure Reports** - Detailed reports for payment failure incidents
5. **Security Incident Reports** - Comprehensive reports for security incidents
6. **Professional PDF Generation** - High-quality PDF reports using QuestPDF
7. **Version Control** - Report versioning for tracking changes

## Features

- ✅ **Template-Based Reports** - Consistent formatting across all report types
- ✅ **Multiple Export Formats** - Markdown, HTML, and PDF
- ✅ **Customizable Sections** - Control which sections appear in reports
- ✅ **Executive Summary** - High-level overview for stakeholders
- ✅ **Incident Timeline** - Chronological event timeline
- ✅ **Root Cause Analysis** - Detailed analysis of incident causes
- ✅ **Impact Assessment** - Evaluation of incident impact
- ✅ **Actions Taken** - Documentation of response actions
- ✅ **Preventive Measures** - Recommendations for preventing future incidents
- ✅ **Lessons Learned** - Key takeaways from the incident
- ✅ **PDF Generation** - Professional PDF reports with QuestPDF
- ✅ **HTML Export** - Formatted HTML for email notifications
- ✅ **Markdown Export** - Markdown format for documentation
- ✅ **Version Control** - Report versioning for tracking
- ✅ **Admin Endpoints** - REST API endpoints for report generation

## Architecture

### Components

1. **IIncidentReportGenerator** (`Payment.Application.Interfaces.IIncidentReportGenerator`)
   - Main interface for incident report generation
   - Located in Application layer following Clean Architecture

2. **IncidentReportGenerator** (`Payment.Application.Services.IncidentReportGenerator`)
   - Core implementation of report generation logic
   - Handles template generation and format conversion

3. **IncidentController** (`Payment.API.Controllers.Admin.IncidentController`)
   - REST API controller for report generation
   - Requires `AdminOnly` authorization policy

4. **QuestPDF Library** - Professional PDF generation library

## Data Models

### IncidentReport

Complete incident report structure:

```csharp
public sealed record IncidentReport
{
    public string ReportId { get; init; }
    public string IncidentId { get; init; }
    public string IncidentType { get; init; } // "PaymentFailure" or "SecurityIncident"
    public string Severity { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string GeneratedBy { get; init; }
    public string Format { get; init; } // "Markdown", "HTML", "PDF"
    public byte[] Content { get; init; }
    public int Version { get; init; }
}
```

### PaymentFailureIncident

Payment failure incident data:

```csharp
public sealed record PaymentFailureIncident
{
    public string IncidentId { get; init; }
    public PaymentFailureContext Context { get; init; }
    public IncidentAssessment Assessment { get; init; }
    public IEnumerable<IncidentTimelineEvent> Timeline { get; init; }
    public string ImpactAssessment { get; init; }
    public IEnumerable<string> ActionsTaken { get; init; }
    public IEnumerable<string> PreventiveMeasures { get; init; }
    public IEnumerable<string> LessonsLearned { get; init; }
}
```

### SecurityIncident

Security incident data:

```csharp
public sealed record SecurityIncident
{
    public string IncidentId { get; init; }
    public SecurityIncidentAssessment Assessment { get; init; }
    public IEnumerable<IncidentTimelineEvent> Timeline { get; init; }
    public string ImpactAssessment { get; init; }
    public IEnumerable<string> ActionsTaken { get; init; }
    public IEnumerable<string> PreventiveMeasures { get; init; }
    public IEnumerable<string> LessonsLearned { get; init; }
    public IEnumerable<string> AffectedUsers { get; init; }
    public IEnumerable<string> CompromisedResources { get; init; }
}
```

### IncidentTimelineEvent

Timeline event representation:

```csharp
public sealed record IncidentTimelineEvent
{
    public DateTime Timestamp { get; init; }
    public string Event { get; init; }
    public string Description { get; init; }
    public string? Actor { get; init; }
}
```

### ReportGenerationOptions

Options for customizing report sections:

```csharp
public sealed record ReportGenerationOptions
{
    public bool IncludeExecutiveSummary { get; init; } = true;
    public bool IncludeTimeline { get; init; } = true;
    public bool IncludeRootCauseAnalysis { get; init; } = true;
    public bool IncludeImpactAssessment { get; init; } = true;
    public bool IncludeActionsTaken { get; init; } = true;
    public bool IncludePreventiveMeasures { get; init; } = true;
    public bool IncludeLessonsLearned { get; init; } = true;
}
```

## Report Sections

### Executive Summary

High-level overview of the incident:
- Incident type and severity
- Root cause summary
- Affected resources/counts
- Duration and status
- Key metrics

### Incident Timeline

Chronological sequence of events:
- Event timestamps
- Event descriptions
- Actors involved
- Actions taken at each step

### Root Cause Analysis

Detailed analysis of incident causes:
- Primary root cause
- Contributing factors
- Technical details
- System interactions

### Impact Assessment

Evaluation of incident impact:
- Affected users/resources
- Business impact
- Financial impact
- Service degradation
- Estimated resolution time

### Actions Taken

Documentation of response actions:
- Immediate containment actions
- Investigation steps
- Remediation actions
- Recovery procedures

### Recommended Actions

Suggested actions from assessment:
- Priority levels (High, Medium, Low)
- Action descriptions
- Estimated time to complete
- Responsible parties

### Preventive Measures

Recommendations for prevention:
- System improvements
- Process changes
- Monitoring enhancements
- Training requirements

### Lessons Learned

Key takeaways from the incident:
- What went well
- What could be improved
- Process improvements
- Tool enhancements

## API Endpoints

All endpoints require `AdminOnly` authorization policy (requires `payment.admin` scope).

### Generate Payment Failure Report

Generate a report for a payment failure incident.

**Endpoint:** `POST /api/v1/admin/incidents/payment-failure/report`

**Query Parameters:**
- `format` (string, default: "markdown") - Report format: "markdown", "html", or "pdf"
- `includeExecutiveSummary` (bool, default: true) - Include executive summary section
- `includeTimeline` (bool, default: true) - Include timeline section
- `includeRootCauseAnalysis` (bool, default: true) - Include root cause analysis section
- `includeImpactAssessment` (bool, default: true) - Include impact assessment section
- `includeActionsTaken` (bool, default: true) - Include actions taken section
- `includePreventiveMeasures` (bool, default: true) - Include preventive measures section
- `includeLessonsLearned` (bool, default: true) - Include lessons learned section

**Request Body:**
```json
{
  "incidentId": "incident-123",
  "context": {
    "startTime": "2025-01-15T10:00:00Z",
    "endTime": "2025-01-15T10:30:00Z",
    "provider": "Stripe",
    "failureType": "ProviderUnavailable",
    "affectedPaymentCount": 150,
    "metadata": {
      "circuitBreakerOpen": true,
      "errorRate": 0.95
    }
  },
  "assessment": {
    "severity": "High",
    "rootCause": "Payment provider API outage",
    "affectedProviders": ["Stripe"],
    "affectedPaymentCount": 150,
    "estimatedResolutionTime": "00:30:00",
    "recommendedActions": [
      {
        "action": "SwitchProvider",
        "description": "Switch to alternative payment provider: Checkout",
        "priority": "High",
        "estimatedTime": "5 minutes"
      }
    ]
  },
  "timeline": [
    {
      "timestamp": "2025-01-15T10:00:00Z",
      "event": "Incident Detected",
      "description": "Circuit breaker opened for Stripe provider",
      "actor": "System"
    },
    {
      "timestamp": "2025-01-15T10:05:00Z",
      "event": "Assessment Completed",
      "description": "Incident severity assessed as High",
      "actor": "IncidentResponseService"
    }
  ],
  "impactAssessment": "150 payments affected, estimated revenue impact of $15,000",
  "actionsTaken": [
    "Switched to backup provider Checkout",
    "Notified payment operations team",
    "Monitored recovery"
  ],
  "preventiveMeasures": [
    "Implement additional backup providers",
    "Enhance circuit breaker monitoring",
    "Improve provider health checks"
  ],
  "lessonsLearned": [
    "Need faster failover to backup providers",
    "Circuit breaker thresholds may need adjustment"
  ]
}
```

**Example Request:**
```http
POST /api/v1/admin/incidents/payment-failure/report?format=pdf&includeExecutiveSummary=true
Authorization: Bearer <token>
Content-Type: application/json

{
  "incidentId": "incident-123",
  ...
}
```

**Response:** File download (Markdown, HTML, or PDF based on format parameter)

### Generate Security Incident Report

Generate a report for a security incident.

**Endpoint:** `POST /api/v1/admin/incidents/security/report`

**Query Parameters:** Same as payment failure report endpoint

**Request Body:**
```json
{
  "incidentId": "security-incident-456",
  "assessment": {
    "severity": "High",
    "threatType": "UnauthorizedAccess",
    "affectedResources": ["Payment API", "Database"],
    "compromisedCredentials": ["api-key-123"],
    "recommendedContainment": {
      "strategy": "BlockIpAddress",
      "parameters": {
        "ipAddress": "10.0.0.1"
      }
    },
    "remediationActions": [
      {
        "action": "RevokeCredentials",
        "description": "Revoke compromised credential: api-key-123",
        "priority": "High",
        "estimatedTime": "Immediate"
      }
    ]
  },
  "timeline": [
    {
      "timestamp": "2025-01-15T10:00:00Z",
      "event": "Unauthorized Access Detected",
      "description": "Invalid API key used from IP 10.0.0.1",
      "actor": "SecuritySystem"
    }
  ],
  "impactAssessment": "Unauthorized access attempt detected, no data compromised",
  "actionsTaken": [
    "Blocked IP address 10.0.0.1",
    "Revoked compromised API key",
    "Notified security team"
  ],
  "preventiveMeasures": [
    "Enhance API key rotation policy",
    "Implement additional IP whitelisting",
    "Improve monitoring for suspicious activity"
  ],
  "lessonsLearned": [
    "API key rotation should be more frequent",
    "Need better detection of credential compromise"
  ],
  "affectedUsers": ["user-123"],
  "compromisedResources": ["api-key-123"]
}
```

**Example Request:**
```http
POST /api/v1/admin/incidents/security/report?format=pdf
Authorization: Bearer <token>
Content-Type: application/json

{
  "incidentId": "security-incident-456",
  ...
}
```

**Response:** File download (Markdown, HTML, or PDF based on format parameter)

## Report Formats

### Markdown Format

Markdown format is the default and provides:
- Structured sections with headers
- Bullet points for lists
- Bold text for emphasis
- Code blocks for technical details
- Easy to read and edit

**Use Cases:**
- Documentation
- Version control
- Easy editing
- Plain text storage

### HTML Format

HTML format provides:
- Formatted text with styling
- Professional appearance
- Suitable for email notifications
- Web-friendly format

**Use Cases:**
- Email notifications
- Web display
- Sharing with stakeholders
- Professional presentation

### PDF Format

PDF format provides:
- Professional appearance
- Consistent formatting
- Print-ready
- Secure distribution
- Page numbers and headers

**Use Cases:**
- Formal reports
- Compliance documentation
- Executive presentations
- Archival storage

## Usage Examples

### Generate Payment Failure Report (PDF)

```csharp
var incident = new PaymentFailureIncident
{
    IncidentId = "incident-123",
    Context = paymentFailureContext,
    Assessment = incidentAssessment,
    Timeline = timelineEvents,
    ImpactAssessment = "150 payments affected",
    ActionsTaken = new[] { "Switched to backup provider", "Notified team" },
    PreventiveMeasures = new[] { "Add more backup providers" },
    LessonsLearned = new[] { "Need faster failover" }
};

var options = new ReportGenerationOptions
{
    IncludeExecutiveSummary = true,
    IncludeTimeline = true,
    IncludeRootCauseAnalysis = true,
    IncludeImpactAssessment = true,
    IncludeActionsTaken = true,
    IncludePreventiveMeasures = true,
    IncludeLessonsLearned = true
};

var report = await _reportGenerator.GeneratePaymentFailureReportAsync(incident, options);
var pdfData = await _reportGenerator.ExportToPdfAsync(report);
```

### Generate Security Incident Report (HTML)

```csharp
var incident = new SecurityIncident
{
    IncidentId = "security-incident-456",
    Assessment = securityAssessment,
    Timeline = timelineEvents,
    ImpactAssessment = "Unauthorized access detected",
    ActionsTaken = new[] { "Blocked IP", "Revoked credentials" },
    PreventiveMeasures = new[] { "Enhance monitoring" },
    LessonsLearned = new[] { "Need better detection" },
    AffectedUsers = new[] { "user-123" },
    CompromisedResources = new[] { "api-key-123" }
};

var report = await _reportGenerator.GenerateSecurityIncidentReportAsync(incident);
var htmlData = await _reportGenerator.ExportToHtmlAsync(report);
```

### Customize Report Sections

```csharp
var options = new ReportGenerationOptions
{
    IncludeExecutiveSummary = true,
    IncludeTimeline = true,
    IncludeRootCauseAnalysis = false, // Exclude root cause analysis
    IncludeImpactAssessment = true,
    IncludeActionsTaken = true,
    IncludePreventiveMeasures = false, // Exclude preventive measures
    IncludeLessonsLearned = true
};

var report = await _reportGenerator.GeneratePaymentFailureReportAsync(incident, options);
```

## Integration with Incident Response

The report generation system integrates seamlessly with:

1. **Incident Response Service** - Uses incident assessment data for reports
2. **Security Incident Response Service** - Uses security assessment data for reports
3. **Audit Log Querying** - Can include audit log data in reports
4. **Runbooks** - Reports can reference runbook procedures

## Best Practices

1. **Include All Sections**: Include all sections for comprehensive reports
2. **Use PDF for Formal Reports**: Use PDF format for compliance and formal documentation
3. **Use HTML for Notifications**: Use HTML format for email notifications
4. **Use Markdown for Documentation**: Use Markdown format for version-controlled documentation
5. **Customize as Needed**: Exclude sections that aren't relevant to specific incidents
6. **Version Control**: Track report versions for audit purposes
7. **Archive Reports**: Store generated reports for compliance and historical reference

## Related Documentation

- [Incident Response Service](./Incident_Response_Service.md) - Payment failure incident handling
- [Security Incident Response Service](./Security_Incident_Response_Service.md) - Security incident handling
- [Audit Log Querying](./Audit_Log_Querying.md) - Querying audit logs for reports
- [Runbooks](../runbooks/README.md) - Incident response procedures

