using System.ComponentModel.DataAnnotations;

namespace Payment.Application.DTOs;

/// <summary>
/// DTO for querying audit logs with filtering, pagination, and sorting.
/// Follows Single Responsibility Principle - only responsible for query parameters.
/// </summary>
public sealed record AuditLogQuery
{
    /// <summary>
    /// Filter by user ID.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Filter by IP address.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Filter by event type/action (e.g., "PaymentCreated", "PaymentRefunded").
    /// </summary>
    public string? EventType { get; init; }

    /// <summary>
    /// Filter by resource/entity type (e.g., "Payment", "Refund").
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// Filter by entity ID.
    /// </summary>
    public Guid? EntityId { get; init; }

    /// <summary>
    /// Start time for time range filter (UTC).
    /// </summary>
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// End time for time range filter (UTC).
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Full-text search query (searches in action, entity type, and changes).
    /// </summary>
    public string? SearchQuery { get; init; }

    /// <summary>
    /// Page number (1-based).
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>
    /// Page size (max 1000).
    /// </summary>
    [Range(1, 1000)]
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Sort field (default: "Timestamp").
    /// </summary>
    public string? SortBy { get; init; } = "Timestamp";

    /// <summary>
    /// Sort direction (default: "desc").
    /// </summary>
    public string? SortDirection { get; init; } = "desc";
}

/// <summary>
/// DTO representing an audit log entry.
/// </summary>
public sealed record AuditLogEntryDto
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public Dictionary<string, object> Changes { get; init; } = new();
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// DTO for paginated audit log results.
/// </summary>
public sealed record AuditLogQueryResult
{
    public IEnumerable<AuditLogEntryDto> Entries { get; init; } = Array.Empty<AuditLogEntryDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// DTO for audit log summary statistics.
/// </summary>
public sealed record AuditLogSummary
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int TotalEvents { get; init; }
    public int UniqueUsers { get; init; }
    public int UniqueIpAddresses { get; init; }
    public Dictionary<string, int> EventsByType { get; init; } = new();
    public Dictionary<string, int> EventsByResource { get; init; } = new();
    public Dictionary<string, int> TopUsers { get; init; } = new();
    public Dictionary<string, int> TopIpAddresses { get; init; } = new();
}

/// <summary>
/// DTO for security event query.
/// </summary>
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

/// <summary>
/// DTO representing a security event.
/// </summary>
public sealed record SecurityEventDto
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTime Timestamp { get; init; }
}

