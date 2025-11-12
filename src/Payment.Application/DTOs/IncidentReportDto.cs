namespace Payment.Application.DTOs;

/// <summary>
/// DTO representing a complete incident report.
/// </summary>
public sealed record IncidentReport
{
    public string ReportId { get; init; } = string.Empty;
    public string IncidentId { get; init; } = string.Empty;
    public string IncidentType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public string GeneratedBy { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty; // Markdown, HTML, PDF
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public int Version { get; init; } = 1;
}

/// <summary>
/// DTO for payment failure incident data.
/// </summary>
public sealed record PaymentFailureIncident
{
    public string IncidentId { get; init; } = string.Empty;
    public PaymentFailureContext Context { get; init; } = null!;
    public IncidentAssessment Assessment { get; init; } = null!;
    public IEnumerable<IncidentTimelineEvent> Timeline { get; init; } = Array.Empty<IncidentTimelineEvent>();
    public string ImpactAssessment { get; init; } = string.Empty;
    public IEnumerable<string> ActionsTaken { get; init; } = Array.Empty<string>();
    public IEnumerable<string> PreventiveMeasures { get; init; } = Array.Empty<string>();
    public IEnumerable<string> LessonsLearned { get; init; } = Array.Empty<string>();
}

/// <summary>
/// DTO for security incident data.
/// </summary>
public sealed record SecurityIncident
{
    public string IncidentId { get; init; } = string.Empty;
    public SecurityIncidentAssessment Assessment { get; init; } = null!;
    public IEnumerable<IncidentTimelineEvent> Timeline { get; init; } = Array.Empty<IncidentTimelineEvent>();
    public string ImpactAssessment { get; init; } = string.Empty;
    public IEnumerable<string> ActionsTaken { get; init; } = Array.Empty<string>();
    public IEnumerable<string> PreventiveMeasures { get; init; } = Array.Empty<string>();
    public IEnumerable<string> LessonsLearned { get; init; } = Array.Empty<string>();
    public IEnumerable<string> AffectedUsers { get; init; } = Array.Empty<string>();
    public IEnumerable<string> CompromisedResources { get; init; } = Array.Empty<string>();
}

/// <summary>
/// DTO for incident timeline event.
/// </summary>
public sealed record IncidentTimelineEvent
{
    public DateTime Timestamp { get; init; }
    public string Event { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Actor { get; init; }
}

/// <summary>
/// DTO for report generation options.
/// </summary>
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

