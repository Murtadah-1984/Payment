using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;

namespace Payment.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that retries failed webhook deliveries with exponential backoff.
/// Implements the retry pattern to ensure webhooks are eventually delivered.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class WebhookRetryService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookRetryService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    public WebhookRetryService(
        IServiceProvider serviceProvider,
        ILogger<WebhookRetryService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook retry service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingWebhooksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending webhooks");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Webhook retry service stopped");
    }

    private async Task ProcessPendingWebhooksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var webhookRepository = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
        var webhookService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Get webhooks ready for retry
        var pendingWebhooks = (await webhookRepository.GetPendingRetriesAsync(cancellationToken))
            .Take(BatchSize)
            .ToList();

        if (!pendingWebhooks.Any())
        {
            return;
        }

        _logger.LogInformation("Processing {Count} pending webhook retries", pendingWebhooks.Count);

        foreach (var webhook in pendingWebhooks)
        {
            try
            {
                _logger.LogInformation(
                    "Retrying webhook delivery. WebhookId: {WebhookId}, PaymentId: {PaymentId}, EventType: {EventType}, Attempt: {RetryCount}/{MaxRetries}",
                    webhook.Id, webhook.PaymentId, webhook.EventType, webhook.RetryCount + 1, webhook.MaxRetries);

                // Attempt to deliver the webhook
                var result = await webhookService.SendWebhookAsync(
                    webhook.WebhookUrl,
                    webhook.EventType,
                    webhook.Payload,
                    cancellationToken: cancellationToken);

                if (result.Success)
                {
                    webhook.MarkAsDelivered();
                    _logger.LogInformation(
                        "Webhook delivered successfully after {RetryCount} retries. WebhookId: {WebhookId}",
                        webhook.RetryCount, webhook.Id);
                }
                else
                {
                    webhook.MarkAsFailed(result.ErrorMessage ?? "Unknown error", result.HttpStatusCode);
                    _logger.LogWarning(
                        "Webhook delivery failed. WebhookId: {WebhookId}, Error: {Error}, NextRetryAt: {NextRetryAt}",
                        webhook.Id, result.ErrorMessage, webhook.NextRetryAt);
                }

                await webhookRepository.UpdateAsync(webhook, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrying webhook delivery. WebhookId: {WebhookId}, PaymentId: {PaymentId}",
                    webhook.Id, webhook.PaymentId);

                // Mark as failed with exception message
                webhook.MarkAsFailed(ex.Message, null);
                await webhookRepository.UpdateAsync(webhook, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}

