using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Payment.Domain.Entities;
using Payment.Domain.Events;
using Payment.Domain.Interfaces;
using System.Text.Json;

namespace Payment.Application.Services;

/// <summary>
/// Service that schedules webhooks when payment status changes occur.
/// Follows Single Responsibility Principle - only handles webhook scheduling.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class PaymentWebhookNotifier : IPaymentWebhookNotifier
{
    private readonly IWebhookDeliveryService _webhookDeliveryService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentWebhookNotifier> _logger;

    public PaymentWebhookNotifier(
        IWebhookDeliveryService webhookDeliveryService,
        IConfiguration configuration,
        ILogger<PaymentWebhookNotifier> logger)
    {
        _webhookDeliveryService = webhookDeliveryService ?? throw new ArgumentNullException(nameof(webhookDeliveryService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyPaymentStatusChangeAsync(
        Payment payment,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (payment == null)
            throw new ArgumentNullException(nameof(payment));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));

        // Get webhook URL from payment metadata or configuration
        var webhookUrl = GetWebhookUrl(payment);

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogDebug(
                "No webhook URL configured for payment {PaymentId}. Skipping webhook notification.",
                payment.Id.Value);
            return;
        }

        // Create webhook payload
        var payload = CreateWebhookPayload(payment, eventType);

        try
        {
            // Schedule webhook for delivery with retry mechanism
            var webhookId = await _webhookDeliveryService.ScheduleWebhookAsync(
                paymentId: payment.Id.Value,
                webhookUrl: webhookUrl,
                eventType: eventType,
                payload: payload,
                maxRetries: GetMaxRetries(),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Webhook scheduled for payment status change. PaymentId: {PaymentId}, EventType: {EventType}, WebhookId: {WebhookId}",
                payment.Id.Value, eventType, webhookId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to schedule webhook for payment {PaymentId}, EventType: {EventType}",
                payment.Id.Value, eventType);
            // Don't throw - webhook failure shouldn't break payment processing
        }
    }

    private string? GetWebhookUrl(Payment payment)
    {
        // Priority 1: Check payment metadata for webhook URL
        if (payment.Metadata.TryGetValue("webhook_url", out var webhookUrl) ||
            payment.Metadata.TryGetValue("webhookUrl", out webhookUrl) ||
            payment.Metadata.TryGetValue("callback_url", out webhookUrl) ||
            payment.Metadata.TryGetValue("callbackUrl", out webhookUrl))
        {
            if (!string.IsNullOrWhiteSpace(webhookUrl) && Uri.TryCreate(webhookUrl, UriKind.Absolute, out _))
            {
                return webhookUrl;
            }
        }

        // Priority 2: Check configuration for merchant-specific webhook URL
        var merchantWebhookUrl = _configuration[$"Webhooks:Merchants:{payment.MerchantId}:Url"];
        if (!string.IsNullOrWhiteSpace(merchantWebhookUrl) && Uri.TryCreate(merchantWebhookUrl, UriKind.Absolute, out _))
        {
            return merchantWebhookUrl;
        }

        // Priority 3: Check configuration for default webhook URL
        var defaultWebhookUrl = _configuration["Webhooks:DefaultUrl"];
        if (!string.IsNullOrWhiteSpace(defaultWebhookUrl) && Uri.TryCreate(defaultWebhookUrl, UriKind.Absolute, out _))
        {
            return defaultWebhookUrl;
        }

        return null;
    }

    private string CreateWebhookPayload(Payment payment, string eventType)
    {
        var payload = new
        {
            EventType = eventType,
            PaymentId = payment.Id.Value,
            OrderId = payment.OrderId,
            MerchantId = payment.MerchantId,
            Amount = payment.Amount.Value,
            Currency = payment.Currency.Code,
            Status = payment.Status.ToString(),
            TransactionId = payment.TransactionId,
            FailureReason = payment.FailureReason,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt,
            Metadata = payment.Metadata
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private int GetMaxRetries()
    {
        var maxRetries = _configuration.GetValue<int>("Webhooks:MaxRetries", 5);
        return Math.Max(1, Math.Min(maxRetries, 10)); // Clamp between 1 and 10
    }
}

/// <summary>
/// Interface for notifying external systems about payment status changes via webhooks.
/// </summary>
public interface IPaymentWebhookNotifier
{
    /// <summary>
    /// Schedules a webhook notification for a payment status change.
    /// </summary>
    Task NotifyPaymentStatusChangeAsync(
        Payment payment,
        string eventType,
        CancellationToken cancellationToken = default);
}

