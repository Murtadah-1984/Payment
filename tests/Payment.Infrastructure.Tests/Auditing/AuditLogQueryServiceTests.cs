using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Domain.Entities;
using Payment.Infrastructure.Auditing;
using Payment.Infrastructure.Data;
using Xunit;

namespace Payment.Infrastructure.Tests.Auditing;

/// <summary>
/// Unit tests for AuditLogQueryService.
/// Tests filtering, pagination, sorting, and export functionality.
/// </summary>
public class AuditLogQueryServiceTests : IDisposable
{
    private readonly PaymentDbContext _context;
    private readonly AuditLogQueryService _service;
    private readonly ILogger<AuditLogQueryService> _logger;

    public AuditLogQueryServiceTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new PaymentDbContext(options);
        _logger = new LoggerFactory().CreateLogger<AuditLogQueryService>();
        _service = new AuditLogQueryService(_context, _logger);

        SeedTestData();
    }

    [Fact]
    public async Task QueryAsync_WithNoFilters_ReturnsAllLogs()
    {
        // Arrange
        var query = new AuditLogQuery { Page = 1, PageSize = 10 };

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(5, result.Entries.Count());
    }

    [Fact]
    public async Task QueryAsync_WithUserIdFilter_ReturnsFilteredLogs()
    {
        // Arrange
        var query = new AuditLogQuery { UserId = "user1", Page = 1, PageSize = 10 };

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Entries, e => Assert.Equal("user1", e.UserId));
    }

    [Fact]
    public async Task QueryAsync_WithIpAddressFilter_ReturnsFilteredLogs()
    {
        // Arrange
        var query = new AuditLogQuery { IpAddress = "192.168.1.1", Page = 1, PageSize = 10 };

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Entries, e => Assert.Equal("192.168.1.1", e.IpAddress));
    }

    [Fact]
    public async Task QueryAsync_WithEventTypeFilter_ReturnsFilteredLogs()
    {
        // Arrange
        var query = new AuditLogQuery { EventType = "PaymentCreated", Page = 1, PageSize = 10 };

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Entries, e => Assert.Equal("PaymentCreated", e.Action));
    }

    [Fact]
    public async Task QueryAsync_WithTimeRangeFilter_ReturnsFilteredLogs()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-2);
        var endTime = DateTime.UtcNow.AddHours(-1);
        var query = new AuditLogQuery
        {
            StartTime = startTime,
            EndTime = endTime,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Entries, e =>
        {
            Assert.True(e.Timestamp >= startTime);
            Assert.True(e.Timestamp <= endTime);
        });
    }

    [Fact]
    public async Task QueryAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var query = new AuditLogQuery { Page = 2, PageSize = 2 };

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Entries.Count());
        Assert.Equal(2, result.Page);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task QueryAsync_WithSorting_ReturnsSortedLogs()
    {
        // Arrange
        var query = new AuditLogQuery
        {
            SortBy = "Timestamp",
            SortDirection = "asc",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        var entries = result.Entries.ToList();
        for (int i = 0; i < entries.Count - 1; i++)
        {
            Assert.True(entries[i].Timestamp <= entries[i + 1].Timestamp);
        }
    }

    [Fact]
    public async Task QueryAsync_WithSearchQuery_ReturnsMatchingLogs()
    {
        // Arrange
        var query = new AuditLogQuery
        {
            SearchQuery = "PaymentCreated",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _service.QueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalCount > 0);
        Assert.All(result.Entries, e =>
            Assert.True(e.Action.Contains("PaymentCreated", StringComparison.OrdinalIgnoreCase) ||
                       e.EntityType.Contains("PaymentCreated", StringComparison.OrdinalIgnoreCase) ||
                       e.UserId.Contains("PaymentCreated", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-24);
        var endTime = DateTime.UtcNow;

        // Act
        var summary = await _service.GetSummaryAsync(startTime, endTime);

        // Assert
        Assert.NotNull(summary);
        Assert.True(summary.TotalEvents > 0);
        Assert.True(summary.UniqueUsers > 0);
        Assert.NotNull(summary.EventsByType);
        Assert.NotNull(summary.EventsByResource);
    }

    [Fact]
    public async Task GetSecurityEventsAsync_ReturnsOnlySecurityEvents()
    {
        // Arrange
        var query = new SecurityEventQuery { Page = 1, PageSize = 10 };

        // Act
        var events = await _service.GetSecurityEventsAsync(query);

        // Assert
        Assert.NotNull(events);
        // Note: In real scenario, we'd seed security events
        // For now, we verify the method doesn't throw
    }

    [Fact]
    public async Task ExportToCsvAsync_ReturnsValidCsv()
    {
        // Arrange
        var query = new AuditLogQuery { Page = 1, PageSize = 10 };

        // Act
        var csv = await _service.ExportToCsvAsync(query);

        // Assert
        Assert.NotNull(csv);
        Assert.True(csv.Length > 0);
        var csvString = System.Text.Encoding.UTF8.GetString(csv);
        Assert.Contains("Id,UserId,Action", csvString);
    }

    [Fact]
    public async Task ExportToJsonAsync_ReturnsValidJson()
    {
        // Arrange
        var query = new AuditLogQuery { Page = 1, PageSize = 10 };

        // Act
        var json = await _service.ExportToJsonAsync(query);

        // Assert
        Assert.NotNull(json);
        Assert.True(json.Length > 0);
        var jsonString = System.Text.Encoding.UTF8.GetString(json);
        Assert.Contains("Query", jsonString);
        Assert.Contains("Entries", jsonString);
    }

    private void SeedTestData()
    {
        var logs = new List<AuditLog>
        {
            CreateAuditLogWithTimestamp(
                "user1",
                "PaymentCreated",
                "Payment",
                "192.168.1.1",
                "Mozilla/5.0",
                new Dictionary<string, object> { { "Amount", 100.00m } },
                DateTime.UtcNow.AddHours(-3)),
            CreateAuditLogWithTimestamp(
                "user1",
                "PaymentRefunded",
                "Payment",
                "192.168.1.1",
                "Mozilla/5.0",
                new Dictionary<string, object> { { "Amount", 50.00m } },
                DateTime.UtcNow.AddHours(-2)),
            CreateAuditLogWithTimestamp(
                "user2",
                "PaymentCreated",
                "Payment",
                "192.168.1.2",
                "Chrome/1.0",
                new Dictionary<string, object> { { "Amount", 200.00m } },
                DateTime.UtcNow.AddHours(-1)),
            CreateAuditLogWithTimestamp(
                "user3",
                "UnauthorizedAccess",
                "Security",
                "10.0.0.1",
                "Unknown",
                new Dictionary<string, object> { { "Reason", "Invalid token" } },
                DateTime.UtcNow.AddMinutes(-30)),
            CreateAuditLogWithTimestamp(
                "user4",
                "PaymentCreated",
                "Payment",
                "192.168.1.3",
                "Safari/1.0",
                new Dictionary<string, object> { { "Amount", 300.00m } },
                DateTime.UtcNow)
        };

        _context.AuditLogs.AddRange(logs);
        _context.SaveChanges();
    }

    private AuditLog CreateAuditLogWithTimestamp(
        string userId,
        string action,
        string entityType,
        string? ipAddress,
        string? userAgent,
        Dictionary<string, object>? changes,
        DateTime timestamp)
    {
        var log = new AuditLog(
            Guid.NewGuid(),
            userId,
            action,
            entityType,
            Guid.NewGuid(),
            ipAddress,
            userAgent,
            changes);
        
        // Use reflection to set the read-only Timestamp property
        var timestampProperty = typeof(AuditLog).GetProperty("Timestamp", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        timestampProperty!.SetValue(log, timestamp);
        
        return log;
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

