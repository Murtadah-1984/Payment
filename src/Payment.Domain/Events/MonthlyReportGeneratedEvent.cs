namespace Payment.Domain.Events;

/// <summary>
/// Domain event published when a monthly financial report is generated.
/// This event is consumed by the Notification Microservice to notify system owners.
/// </summary>
public sealed record MonthlyReportGeneratedEvent(
    Guid ReportId,
    int Year,
    int Month,
    string ProjectCode,
    string ReportUrl,
    DateTime GeneratedAtUtc) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

