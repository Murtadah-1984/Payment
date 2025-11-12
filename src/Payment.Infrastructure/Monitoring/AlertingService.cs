using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payment.Application.DTOs;
using Payment.Domain.Enums;
using Payment.Application.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Metrics;
using Payment.Infrastructure.Monitoring.Channels;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Payment.Infrastructure.Monitoring;

/// <summary>
/// Service for sending automated alerts.
/// Follows Single Responsibility Principle - only handles alerting.
/// Stateless by design - suitable for Kubernetes deployment.
/// Implements alert deduplication to prevent alert storms.
/// </summary>
public class AlertingService : IAlertingService
{
    private readonly IEnumerable<IAlertChannel> _channels;
    private readonly AlertRulesConfiguration _alertRules;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AlertingService> _logger;
    private readonly IMetricsRecorder? _metricsRecorder;
    private readonly ConcurrentDictionary<string, DateTime> _alertDeduplicationCache = new();
    private readonly TimeSpan _deduplicationWindow = TimeSpan.FromMinutes(5);

    public AlertingService(
        IEnumerable<IAlertChannel> channels,
        IOptions<AlertRulesConfiguration> alertRules,
        IDistributedCache cache,
        ILogger<AlertingService> logger,
        IMetricsRecorder? metricsRecorder = null)
    {
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _alertRules = alertRules?.Value ?? throw new ArgumentNullException(nameof(alertRules));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsRecorder = metricsRecorder;
    }

    public async Task SendAlertAsync(
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty", nameof(message));

        // Check for alert deduplication
        var alertKey = GenerateAlertKey(severity, title, message);
        if (await IsDuplicateAlertAsync(alertKey, ct))
        {
            _logger.LogWarning(
                "Alert deduplicated. Severity: {Severity}, Title: {Title}",
                severity, title);
            
            // Record deduplication metric
            _metricsRecorder?.RecordAlertDeduplicated(severity.ToString(), "Generic");
            return;
        }

        // Mark alert as sent
        await MarkAlertAsSentAsync(alertKey, ct);

        // Get channels for this severity
        var targetChannels = GetChannelsForSeverity(severity);

        // Send alert through all applicable channels
        var tasks = targetChannels.Select(channel =>
            SendAlertToChannelAsync(channel, severity, title, message, metadata, ct));

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Alert sent. Severity: {Severity}, Title: {Title}, Channels: {ChannelCount}",
            severity, title, targetChannels.Count());
        
        // Record metrics for each channel
        foreach (var channel in targetChannels)
        {
            _metricsRecorder?.RecordAlertSent(severity.ToString(), channel.Name, "Generic");
        }
    }

    public async Task SendPaymentFailureAlertAsync(
        PaymentFailureContext context,
        CancellationToken ct = default)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        // Determine severity based on context
        var severity = DeterminePaymentFailureSeverity(context);
        var title = $"Payment Failure Alert - {context.FailureType}";
        var message = BuildPaymentFailureMessage(context);
        var metadata = new Dictionary<string, object>
        {
            ["Provider"] = context.Provider ?? "Unknown",
            ["FailureType"] = context.FailureType.ToString(),
            ["AffectedCount"] = context.AffectedPaymentCount,
            ["StartTime"] = context.StartTime,
            ["IsOngoing"] = context.IsOngoing
        };

        // Check for deduplication
        var alertKey = GenerateAlertKey(severity, title, message);
        if (await IsDuplicateAlertAsync(alertKey, ct))
        {
            _metricsRecorder?.RecordAlertDeduplicated(severity.ToString(), "PaymentFailure");
            return;
        }

        // Mark alert as sent
        await MarkAlertAsSentAsync(alertKey, ct);

        // Get channels for this severity
        var targetChannels = GetChannelsForSeverity(severity);

        // Send alert through all applicable channels
        var tasks = targetChannels.Select(channel =>
            SendAlertToChannelAsync(channel, severity, title, message, metadata, ct));

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Payment failure alert sent. Severity: {Severity}, Title: {Title}, Channels: {ChannelCount}",
            severity, title, targetChannels.Count());
        
        // Record metrics for payment failure alerts
        foreach (var channel in targetChannels)
        {
            _metricsRecorder?.RecordAlertSent(severity.ToString(), channel.Name, "PaymentFailure");
        }
    }

    public async Task SendSecurityIncidentAlertAsync(
        SecurityEvent securityEvent,
        CancellationToken ct = default)
    {
        if (securityEvent == null)
            throw new ArgumentNullException(nameof(securityEvent));

        // Determine severity based on security event type
        var severity = DetermineSecurityIncidentSeverity(securityEvent);
        var title = $"Security Incident - {securityEvent.EventType}";
        var message = BuildSecurityIncidentMessage(securityEvent);
        var metadata = new Dictionary<string, object>
        {
            ["EventType"] = securityEvent.EventType.ToString(),
            ["Timestamp"] = securityEvent.Timestamp,
            ["UserId"] = securityEvent.UserId ?? "Unknown",
            ["IpAddress"] = securityEvent.IpAddress ?? "Unknown",
            ["Resource"] = securityEvent.Resource,
            ["Action"] = securityEvent.Action,
            ["Succeeded"] = securityEvent.Succeeded,
            ["Details"] = securityEvent.Details ?? string.Empty
        };

        // Check for deduplication
        var alertKey = GenerateAlertKey(severity, title, message);
        if (await IsDuplicateAlertAsync(alertKey, ct))
        {
            _metricsRecorder?.RecordAlertDeduplicated(severity.ToString(), "SecurityIncident");
            return;
        }

        // Mark alert as sent
        await MarkAlertAsSentAsync(alertKey, ct);

        // Get channels for this severity
        var targetChannels = GetChannelsForSeverity(severity);

        // Send alert through all applicable channels
        var tasks = targetChannels.Select(channel =>
            SendAlertToChannelAsync(channel, severity, title, message, metadata, ct));

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Security incident alert sent. Severity: {Severity}, Title: {Title}, Channels: {ChannelCount}",
            severity, title, targetChannels.Count());
        
        // Record metrics for security incident alerts
        foreach (var channel in targetChannels)
        {
            _metricsRecorder?.RecordAlertSent(severity.ToString(), channel.Name, "SecurityIncident");
        }
    }

    private AlertSeverity DeterminePaymentFailureSeverity(PaymentFailureContext context)
    {
        // Check alert rules configuration
        if (_alertRules.PaymentFailure?.Critical != null &&
            context.AffectedPaymentCount > 10)
        {
            return AlertSeverity.Critical;
        }

        if (_alertRules.PaymentFailure?.High != null &&
            context.AffectedPaymentCount > 5)
        {
            return AlertSeverity.High;
        }

        if (_alertRules.PaymentFailure?.Medium != null &&
            context.AffectedPaymentCount > 2)
        {
            return AlertSeverity.Medium;
        }

        return AlertSeverity.Low;
    }

    private AlertSeverity DetermineSecurityIncidentSeverity(SecurityEvent securityEvent)
    {
        // Check alert rules configuration
        if (_alertRules.SecurityIncident?.Critical != null &&
            (securityEvent.EventType == SecurityEventType.DataBreach ||
             securityEvent.EventType == SecurityEventType.CredentialCompromise ||
             securityEvent.EventType == SecurityEventType.UnauthorizedAccess))
        {
            return AlertSeverity.Critical;
        }

        if (_alertRules.SecurityIncident?.High != null &&
            (securityEvent.EventType == SecurityEventType.MaliciousPayload ||
             securityEvent.EventType == SecurityEventType.SuspiciousPaymentPattern))
        {
            return AlertSeverity.High;
        }

        if (_alertRules.SecurityIncident?.Medium != null &&
            (securityEvent.EventType == SecurityEventType.SuspiciousAuthentication ||
             securityEvent.EventType == SecurityEventType.RateLimitExceeded))
        {
            return AlertSeverity.Medium;
        }

        return AlertSeverity.Low;
    }

    private string BuildPaymentFailureMessage(PaymentFailureContext context)
    {
        return $"Payment failure detected. " +
               $"Type: {context.FailureType}, " +
               $"Provider: {context.Provider ?? "Unknown"}, " +
               $"Affected Payments: {context.AffectedPaymentCount}, " +
               $"Started: {context.StartTime:yyyy-MM-dd HH:mm:ss}, " +
               $"Status: {(context.IsOngoing ? "Ongoing" : "Resolved")}";
    }

    private string BuildSecurityIncidentMessage(SecurityEvent securityEvent)
    {
        return $"Security incident detected. " +
               $"Type: {securityEvent.EventType}, " +
               $"Resource: {securityEvent.Resource}, " +
               $"Action: {securityEvent.Action}, " +
               $"Succeeded: {securityEvent.Succeeded}, " +
               $"Timestamp: {securityEvent.Timestamp:yyyy-MM-dd HH:mm:ss}, " +
               $"Details: {securityEvent.Details ?? "N/A"}";
    }

    private IEnumerable<IAlertChannel> GetChannelsForSeverity(AlertSeverity severity)
    {
        return _channels.Where(channel => 
            channel.MinimumSeverity <= severity && 
            IsChannelEnabledForSeverity(channel.Name, severity));
    }

    private bool IsChannelEnabledForSeverity(string channelName, AlertSeverity severity)
    {
        // Check alert rules to see if this channel is enabled for this severity
        var severityName = severity.ToString();
        
        // Check payment failure rules
        var paymentRule = GetRuleForSeverity(_alertRules.PaymentFailure, severityName);
        if (paymentRule?.Channels.Contains(channelName, StringComparer.OrdinalIgnoreCase) == true)
            return true;

        // Check security incident rules
        var securityRule = GetRuleForSeverity(_alertRules.SecurityIncident, severityName);
        if (securityRule?.Channels.Contains(channelName, StringComparer.OrdinalIgnoreCase) == true)
            return true;

        // Default: allow if channel supports the severity
        return true;
    }

    private AlertRule? GetRuleForSeverity(PaymentFailureAlertRules? rules, string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => rules?.Critical,
            "high" => rules?.High,
            "medium" => rules?.Medium,
            "low" => rules?.Low,
            _ => null
        };
    }

    private AlertRule? GetRuleForSeverity(SecurityIncidentAlertRules? rules, string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => rules?.Critical,
            "high" => rules?.High,
            "medium" => rules?.Medium,
            "low" => rules?.Low,
            _ => null
        };
    }

    private async Task SendAlertToChannelAsync(
        IAlertChannel channel,
        AlertSeverity severity,
        string title,
        string message,
        Dictionary<string, object>? metadata,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await channel.SendAsync(severity, title, message, metadata, ct);
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            
            _logger.LogDebug(
                "Alert sent to channel. Channel: {Channel}, Severity: {Severity}",
                channel.Name, severity);
            
            // Record success metrics
            _metricsRecorder?.RecordAlertSendingDuration(channel.Name, severity.ToString(), duration);
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            
            _logger.LogError(ex,
                "Failed to send alert to channel. Channel: {Channel}, Severity: {Severity}",
                channel.Name, severity);
            
            // Record failure metrics
            _metricsRecorder?.RecordAlertChannelFailure(channel.Name, severity.ToString());
            _metricsRecorder?.RecordAlertSendingDuration(channel.Name, severity.ToString(), duration);
            
            // Don't throw - continue with other channels
        }
    }

    private string GenerateAlertKey(AlertSeverity severity, string title, string message)
    {
        // Generate a unique key for deduplication
        var key = $"{severity}:{title}:{message}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key));
    }

    private async Task<bool> IsDuplicateAlertAsync(string alertKey, CancellationToken ct)
    {
        try
        {
            var cached = await _cache.GetStringAsync($"alert:{alertKey}", ct);
            return !string.IsNullOrEmpty(cached);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check alert deduplication cache");
            // Fallback to in-memory cache
            if (_alertDeduplicationCache.TryGetValue(alertKey, out var sentTime))
            {
                if (DateTime.UtcNow - sentTime < _deduplicationWindow)
                    return true;
                _alertDeduplicationCache.TryRemove(alertKey, out _);
            }
            return false;
        }
    }

    private async Task MarkAlertAsSentAsync(string alertKey, CancellationToken ct)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _deduplicationWindow
            };
            await _cache.SetStringAsync(
                $"alert:{alertKey}",
                DateTime.UtcNow.ToString("O"),
                options,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark alert as sent in cache");
            // Fallback to in-memory cache
            _alertDeduplicationCache[alertKey] = DateTime.UtcNow;
        }
    }

    public async Task AcknowledgeAlertAsync(
        string alertKey,
        string acknowledgedBy,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(alertKey))
            throw new ArgumentException("Alert key cannot be null or empty", nameof(alertKey));

        if (string.IsNullOrWhiteSpace(acknowledgedBy))
            throw new ArgumentException("AcknowledgedBy cannot be null or empty", nameof(acknowledgedBy));

        try
        {
            var acknowledgment = new AlertAcknowledgment
            {
                AlertKey = alertKey,
                AcknowledgedBy = acknowledgedBy,
                AcknowledgedAt = DateTime.UtcNow,
                Notes = notes
            };

            var json = System.Text.Json.JsonSerializer.Serialize(acknowledgment);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) // Keep acknowledgments for 30 days
            };

            await _cache.SetStringAsync(
                $"alert:ack:{alertKey}",
                json,
                options,
                ct);

            _logger.LogInformation(
                "Alert acknowledged. AlertKey: {AlertKey}, AcknowledgedBy: {AcknowledgedBy}",
                alertKey, acknowledgedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to acknowledge alert. AlertKey: {AlertKey}",
                alertKey);
            throw;
        }
    }

    public async Task<bool> IsAlertAcknowledgedAsync(
        string alertKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(alertKey))
            throw new ArgumentException("Alert key cannot be null or empty", nameof(alertKey));

        try
        {
            var acknowledged = await _cache.GetStringAsync($"alert:ack:{alertKey}", ct);
            return !string.IsNullOrEmpty(acknowledged);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check alert acknowledgment. AlertKey: {AlertKey}", alertKey);
            return false;
        }
    }
}

