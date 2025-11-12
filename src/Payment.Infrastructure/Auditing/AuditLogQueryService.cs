using Microsoft.EntityFrameworkCore;
using Payment.Application.DTOs;
using Payment.Domain.Entities;
using Payment.Application.Interfaces;
using Payment.Infrastructure.Data;
using System.Text;
using System.Text.Json;

namespace Payment.Infrastructure.Auditing;

/// <summary>
/// Service for querying audit logs with advanced filtering, pagination, and sorting.
/// Follows Single Responsibility Principle - only responsible for audit log queries.
/// Implements Clean Architecture by depending on domain interfaces.
/// </summary>
public class AuditLogQueryService : IAuditLogQueryService
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<AuditLogQueryService> _logger;

    // Security event types that should be flagged
    private static readonly HashSet<string> SecurityEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UnauthorizedAccess",
        "FailedAuthentication",
        "SuspiciousActivity",
        "RateLimitExceeded",
        "InvalidToken",
        "CredentialRevocation",
        "SecurityPolicyViolation"
    };

    public AuditLogQueryService(
        PaymentDbContext context,
        ILogger<AuditLogQueryService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AuditLogQueryResult> QueryAsync(
        AuditLogQuery query,
        CancellationToken ct = default)
    {
        try
        {
            var queryable = _context.AuditLogs.AsQueryable();

            // Apply filters
            queryable = ApplyFilters(queryable, query);

            // Get total count before pagination
            var totalCount = await queryable.CountAsync(ct);

            // Apply sorting
            queryable = ApplySorting(queryable, query);

            // Apply pagination
            var skip = (query.Page - 1) * query.PageSize;
            var entries = await queryable
                .Skip(skip)
                .Take(query.PageSize)
                .ToListAsync(ct);

            var entryDtos = entries.Select(e => new AuditLogEntryDto
            {
                Id = e.Id,
                UserId = e.UserId,
                Action = e.Action,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                IpAddress = e.IpAddress,
                UserAgent = e.UserAgent,
                Changes = e.Changes,
                Timestamp = e.Timestamp
            });

            return new AuditLogQueryResult
            {
                Entries = entryDtos,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying audit logs");
            throw;
        }
    }

    public async Task<AuditLogSummary> GetSummaryAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken ct = default)
    {
        try
        {
            var queryable = _context.AuditLogs
                .Where(a => a.Timestamp >= startTime && a.Timestamp <= endTime);

            var totalEvents = await queryable.CountAsync(ct);
            var uniqueUsers = await queryable.Select(a => a.UserId).Distinct().CountAsync(ct);
            var uniqueIpAddresses = await queryable
                .Where(a => a.IpAddress != null)
                .Select(a => a.IpAddress!)
                .Distinct()
                .CountAsync(ct);

            // Events by type
            var eventsByType = await queryable
                .GroupBy(a => a.Action)
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Action, x => x.Count, ct);

            // Events by resource
            var eventsByResource = await queryable
                .GroupBy(a => a.EntityType)
                .Select(g => new { EntityType = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EntityType, x => x.Count, ct);

            // Top users
            var topUsers = await queryable
                .GroupBy(a => a.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

            // Top IP addresses
            var topIpAddresses = await queryable
                .Where(a => a.IpAddress != null)
                .GroupBy(a => a.IpAddress!)
                .Select(g => new { IpAddress = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToDictionaryAsync(x => x.IpAddress, x => x.Count, ct);

            return new AuditLogSummary
            {
                StartTime = startTime,
                EndTime = endTime,
                TotalEvents = totalEvents,
                UniqueUsers = uniqueUsers,
                UniqueIpAddresses = uniqueIpAddresses,
                EventsByType = eventsByType,
                EventsByResource = eventsByResource,
                TopUsers = topUsers,
                TopIpAddresses = topIpAddresses
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit log summary for {StartTime} to {EndTime}", startTime, endTime);
            throw;
        }
    }

    public async Task<IEnumerable<SecurityEventDto>> GetSecurityEventsAsync(
        SecurityEventQuery query,
        CancellationToken ct = default)
    {
        try
        {
            var queryable = _context.AuditLogs.AsQueryable();

            // Filter for security-related events
            queryable = queryable.Where(a => SecurityEventTypes.Contains(a.Action) ||
                                            a.Action.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
                                            a.Action.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                            a.Action.Contains("Failed", StringComparison.OrdinalIgnoreCase));

            // Apply additional filters
            if (!string.IsNullOrWhiteSpace(query.UserId))
            {
                queryable = queryable.Where(a => a.UserId == query.UserId);
            }

            if (!string.IsNullOrWhiteSpace(query.IpAddress))
            {
                queryable = queryable.Where(a => a.IpAddress == query.IpAddress);
            }

            if (!string.IsNullOrWhiteSpace(query.EventType))
            {
                queryable = queryable.Where(a => a.Action == query.EventType);
            }

            if (query.StartTime.HasValue)
            {
                queryable = queryable.Where(a => a.Timestamp >= query.StartTime.Value);
            }

            if (query.EndTime.HasValue)
            {
                queryable = queryable.Where(a => a.Timestamp <= query.EndTime.Value);
            }

            // Apply pagination
            var skip = (query.Page - 1) * query.PageSize;
            var events = await queryable
                .OrderByDescending(a => a.Timestamp)
                .Skip(skip)
                .Take(query.PageSize)
                .ToListAsync(ct);

            return events.Select(e => new SecurityEventDto
            {
                Id = e.Id,
                UserId = e.UserId,
                EventType = e.Action,
                Severity = DetermineSeverity(e.Action),
                IpAddress = e.IpAddress,
                UserAgent = e.UserAgent,
                Description = $"Security event: {e.Action} on {e.EntityType}",
                Metadata = e.Changes,
                Timestamp = e.Timestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying security events");
            throw;
        }
    }

    public async Task<byte[]> ExportToCsvAsync(
        AuditLogQuery query,
        CancellationToken ct = default)
    {
        try
        {
            var result = await QueryAsync(query with { PageSize = 10000 }, ct); // Large page size for export

            var csv = new StringBuilder();
            csv.AppendLine("Id,UserId,Action,EntityType,EntityId,IpAddress,UserAgent,Timestamp,Changes");

            foreach (var entry in result.Entries)
            {
                var changesJson = JsonSerializer.Serialize(entry.Changes);
                csv.AppendLine($"{entry.Id},{EscapeCsv(entry.UserId)},{EscapeCsv(entry.Action)}," +
                             $"{EscapeCsv(entry.EntityType)},{entry.EntityId},{EscapeCsv(entry.IpAddress ?? "")}," +
                             $"{EscapeCsv(entry.UserAgent ?? "")},{entry.Timestamp:O},{EscapeCsv(changesJson)}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to CSV");
            throw;
        }
    }

    public async Task<byte[]> ExportToJsonAsync(
        AuditLogQuery query,
        CancellationToken ct = default)
    {
        try
        {
            var result = await QueryAsync(query with { PageSize = 10000 }, ct); // Large page size for export

            var exportData = new
            {
                Query = query,
                TotalCount = result.TotalCount,
                ExportedAt = DateTime.UtcNow,
                Entries = result.Entries
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return Encoding.UTF8.GetBytes(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to JSON");
            throw;
        }
    }

    private IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> queryable, AuditLogQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            queryable = queryable.Where(a => a.UserId == query.UserId);
        }

        if (!string.IsNullOrWhiteSpace(query.IpAddress))
        {
            queryable = queryable.Where(a => a.IpAddress == query.IpAddress);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            queryable = queryable.Where(a => a.Action == query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(query.Resource))
        {
            queryable = queryable.Where(a => a.EntityType == query.Resource);
        }

        if (query.EntityId.HasValue)
        {
            queryable = queryable.Where(a => a.EntityId == query.EntityId.Value);
        }

        if (query.StartTime.HasValue)
        {
            queryable = queryable.Where(a => a.Timestamp >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            queryable = queryable.Where(a => a.Timestamp <= query.EndTime.Value);
        }

        // Full-text search (searches in action, entity type, and changes JSON)
        if (!string.IsNullOrWhiteSpace(query.SearchQuery))
        {
            var searchTerm = query.SearchQuery.ToLower();
            queryable = queryable.Where(a =>
                a.Action.ToLower().Contains(searchTerm) ||
                a.EntityType.ToLower().Contains(searchTerm) ||
                a.UserId.ToLower().Contains(searchTerm));
        }

        return queryable;
    }

    private IQueryable<AuditLog> ApplySorting(IQueryable<AuditLog> queryable, AuditLogQuery query)
    {
        var sortBy = query.SortBy?.ToLower() ?? "timestamp";
        var sortDirection = query.SortDirection?.ToLower() ?? "desc";

        queryable = sortBy switch
        {
            "timestamp" => sortDirection == "asc"
                ? queryable.OrderBy(a => a.Timestamp)
                : queryable.OrderByDescending(a => a.Timestamp),
            "userid" => sortDirection == "asc"
                ? queryable.OrderBy(a => a.UserId)
                : queryable.OrderByDescending(a => a.UserId),
            "action" => sortDirection == "asc"
                ? queryable.OrderBy(a => a.Action)
                : queryable.OrderByDescending(a => a.Action),
            "entitytype" => sortDirection == "asc"
                ? queryable.OrderBy(a => a.EntityType)
                : queryable.OrderByDescending(a => a.EntityType),
            _ => queryable.OrderByDescending(a => a.Timestamp)
        };

        return queryable;
    }

    private static string DetermineSeverity(string action)
    {
        if (action.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Breach", StringComparison.OrdinalIgnoreCase))
        {
            return "Critical";
        }

        if (action.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Suspicious", StringComparison.OrdinalIgnoreCase))
        {
            return "High";
        }

        if (action.Contains("RateLimit", StringComparison.OrdinalIgnoreCase))
        {
            return "Medium";
        }

        return "Low";
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // If value contains comma, quote, or newline, wrap in quotes and escape quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

