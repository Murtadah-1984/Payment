using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically cleans up expired idempotency records.
/// Prevents database bloat by removing records older than 24 hours.
/// Follows Single Responsibility Principle - only handles cleanup.
/// </summary>
public class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdempotencyCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Run every 6 hours
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24); // Keep records for 24 hours

    public IdempotencyCleanupService(
        IServiceProvider serviceProvider,
        ILogger<IdempotencyCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Idempotency cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredRecordsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired idempotency records");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Idempotency cleanup service stopped");
    }

    private async Task CleanupExpiredRecordsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoffDate = DateTime.UtcNow.Subtract(_retentionPeriod);
        var deletedCount = await unitOfWork.IdempotentRequests.DeleteExpiredAsync(cutoffDate, cancellationToken);

        if (deletedCount > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Cleaned up {Count} expired idempotency records older than {CutoffDate}",
                deletedCount, cutoffDate);
        }
        else
        {
            _logger.LogDebug("No expired idempotency records to clean up");
        }
    }
}


