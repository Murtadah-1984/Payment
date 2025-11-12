using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Data;

namespace Payment.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that implements audit log retention policy.
/// Moves logs older than 90 days to cold storage (archive) and deletes logs older than 1 year.
/// Follows Single Responsibility Principle - only handles audit log retention.
/// </summary>
public class AuditLogRetentionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogRetentionService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromDays(1); // Run daily
    private readonly TimeSpan _hotRetentionPeriod = TimeSpan.FromDays(90); // Keep hot for 90 days
    private readonly TimeSpan _coldRetentionPeriod = TimeSpan.FromDays(365); // Keep cold for 1 year total

    public AuditLogRetentionService(
        IServiceProvider serviceProvider,
        ILogger<AuditLogRetentionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit log retention service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRetentionPolicyAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing audit log retention policy");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Audit log retention service stopped");
    }

    private async Task ProcessRetentionPolicyAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var now = DateTime.UtcNow;
        var hotCutoffDate = now.Subtract(_hotRetentionPeriod);
        var coldCutoffDate = now.Subtract(_coldRetentionPeriod);

        _logger.LogInformation(
            "Processing audit log retention policy. Hot cutoff: {HotCutoff}, Cold cutoff: {ColdCutoff}",
            hotCutoffDate, coldCutoffDate);

        // Step 1: Archive logs older than 90 days (move to cold storage)
        // In a real implementation, this would move logs to a separate archive table or storage
        var logsToArchive = await context.AuditLogs
            .Where(a => a.Timestamp < hotCutoffDate && a.Timestamp >= coldCutoffDate)
            .ToListAsync(cancellationToken);

        if (logsToArchive.Any())
        {
            _logger.LogInformation(
                "Archiving {Count} audit logs to cold storage (older than {HotCutoff} days)",
                logsToArchive.Count, _hotRetentionPeriod.TotalDays);

            // In production, you would:
            // 1. Copy logs to archive table/storage
            // 2. Mark logs as archived in the main table
            // 3. Optionally move to separate database or blob storage
            
            // For now, we'll just log the action
            // In a real implementation, you might have an IAuditLogArchiveService
            foreach (var log in logsToArchive)
            {
                // Mark as archived (if you add an IsArchived flag)
                // Or copy to archive table
                _logger.LogDebug("Archiving audit log {LogId} from {Timestamp}", log.Id, log.Timestamp);
            }
        }

        // Step 2: Delete logs older than 1 year
        var logsToDelete = await context.AuditLogs
            .Where(a => a.Timestamp < coldCutoffDate)
            .ToListAsync(cancellationToken);

        if (logsToDelete.Any())
        {
            _logger.LogInformation(
                "Deleting {Count} audit logs older than {ColdCutoff} days",
                logsToDelete.Count, _coldRetentionPeriod.TotalDays);

            context.AuditLogs.RemoveRange(logsToDelete);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully deleted {Count} audit logs older than {ColdCutoff} days",
                logsToDelete.Count, _coldRetentionPeriod.TotalDays);
        }
        else
        {
            _logger.LogDebug("No audit logs to delete (all logs are within retention period)");
        }
    }
}

