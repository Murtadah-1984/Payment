namespace Payment.Application.DTOs;

/// <summary>
/// DTO for incident metrics.
/// </summary>
public sealed record IncidentMetricsDto(
    int TotalIncidents,
    int CriticalIncidents,
    int HighSeverityIncidents,
    int MediumSeverityIncidents,
    int LowSeverityIncidents,
    TimeSpan AverageResolutionTime,
    Dictionary<string, int> IncidentsByType)
{
    public static IncidentMetricsDto Create(
        int totalIncidents,
        int criticalIncidents,
        int highSeverityIncidents,
        int mediumSeverityIncidents,
        int lowSeverityIncidents,
        TimeSpan averageResolutionTime,
        Dictionary<string, int>? incidentsByType = null) =>
        new(
            TotalIncidents: totalIncidents,
            CriticalIncidents: criticalIncidents,
            HighSeverityIncidents: highSeverityIncidents,
            MediumSeverityIncidents: mediumSeverityIncidents,
            LowSeverityIncidents: lowSeverityIncidents,
            AverageResolutionTime: averageResolutionTime,
            IncidentsByType: incidentsByType ?? new Dictionary<string, int>());
}
