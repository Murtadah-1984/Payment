using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Payment.API.HealthChecks;

/// <summary>
/// Custom health check for disk space.
/// Checks if there's sufficient disk space available (default: 10% free space required).
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private readonly double _minimumFreeSpacePercent;

    public DiskSpaceHealthCheck(
        ILogger<DiskSpaceHealthCheck> logger,
        double minimumFreeSpacePercent = 10.0)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _minimumFreeSpacePercent = minimumFreeSpacePercent;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var drive = new DriveInfo(System.IO.Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
            
            if (!drive.IsReady)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Drive {drive.Name} is not ready"));
            }

            var totalSpace = drive.TotalSize;
            var freeSpace = drive.AvailableFreeSpace;
            var usedSpace = totalSpace - freeSpace;
            var freeSpacePercent = (double)freeSpace / totalSpace * 100;

            var data = new Dictionary<string, object>
            {
                { "Drive", drive.Name },
                { "TotalSpaceBytes", totalSpace },
                { "FreeSpaceBytes", freeSpace },
                { "UsedSpaceBytes", usedSpace },
                { "FreeSpacePercent", Math.Round(freeSpacePercent, 2) },
                { "MinimumFreeSpacePercent", _minimumFreeSpacePercent }
            };

            if (freeSpacePercent < _minimumFreeSpacePercent)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Disk space is low: {Math.Round(freeSpacePercent, 2)}% free (minimum: {_minimumFreeSpacePercent}%)",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space is healthy: {Math.Round(freeSpacePercent, 2)}% free",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking disk space");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Error checking disk space",
                ex,
                data: new Dictionary<string, object> { { "Error", ex.Message } }));
        }
    }
}

