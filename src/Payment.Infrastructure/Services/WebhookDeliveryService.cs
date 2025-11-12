using Microsoft.Extensions.Logging;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Payment.Infrastructure.Services;

/// <summary>
/// Service for delivering webhooks to external systems with exponential backoff retry mechanism.
/// Follows Single Responsibility Principle - only handles webhook delivery.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class WebhookDeliveryService : IWebhookDeliveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookDeliveryRepository _repository;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public WebhookDeliveryService(
        IHttpClientFactory httpClientFactory,
        IWebhookDeliveryRepository repository,
        ILogger<WebhookDeliveryService> logger,
        IUnitOfWork unitOfWork)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<WebhookDeliveryResult> SendWebhookAsync(
        string webhookUrl,
        string eventType,
        string payload,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("Webhook URL cannot be null or empty", nameof(webhookUrl));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));

        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be null or empty", nameof(payload));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var httpClient = _httpClientFactory.CreateClient("WebhookDelivery");

        try
        {
            // Configure timeout
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Create request
            var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            // Add default headers
            request.Headers.Add("X-Event-Type", eventType);
            request.Headers.Add("User-Agent", "Payment-Service/1.0");

            // Add custom headers if provided
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (request.Headers.Contains(header.Key))
                        request.Headers.Remove(header.Key);
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            // Send webhook
            var response = await httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var responseTime = stopwatch.Elapsed;
            var isSuccess = response.IsSuccessStatusCode;

            if (isSuccess)
            {
                _logger.LogInformation(
                    "Webhook delivered successfully to {WebhookUrl}. Event: {EventType}, StatusCode: {StatusCode}, ResponseTime: {ResponseTime}ms",
                    webhookUrl, eventType, (int)response.StatusCode, responseTime.TotalMilliseconds);

                return new WebhookDeliveryResult(
                    Success: true,
                    HttpStatusCode: (int)response.StatusCode,
                    ResponseTime: responseTime);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Webhook delivery failed to {WebhookUrl}. Event: {EventType}, StatusCode: {StatusCode}, Error: {Error}",
                    webhookUrl, eventType, (int)response.StatusCode, errorContent);

                return new WebhookDeliveryResult(
                    Success: false,
                    HttpStatusCode: (int)response.StatusCode,
                    ErrorMessage: $"HTTP {(int)response.StatusCode}: {errorContent}",
                    ResponseTime: responseTime);
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Webhook delivery timeout to {WebhookUrl}. Event: {EventType}, ResponseTime: {ResponseTime}ms",
                webhookUrl, eventType, stopwatch.Elapsed.TotalMilliseconds);

            return new WebhookDeliveryResult(
                Success: false,
                ErrorMessage: "Request timeout",
                ResponseTime: stopwatch.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Webhook delivery HTTP error to {WebhookUrl}. Event: {EventType}",
                webhookUrl, eventType);

            return new WebhookDeliveryResult(
                Success: false,
                ErrorMessage: ex.Message,
                ResponseTime: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Unexpected error delivering webhook to {WebhookUrl}. Event: {EventType}",
                webhookUrl, eventType);

            return new WebhookDeliveryResult(
                Success: false,
                ErrorMessage: ex.Message,
                ResponseTime: stopwatch.Elapsed);
        }
    }

    public async Task<Guid> ScheduleWebhookAsync(
        Guid paymentId,
        string webhookUrl,
        string eventType,
        string payload,
        int maxRetries = 5,
        CancellationToken cancellationToken = default)
    {
        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment ID cannot be empty", nameof(paymentId));

        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("Webhook URL cannot be null or empty", nameof(webhookUrl));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));

        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be null or empty", nameof(payload));

        if (maxRetries < 0)
            throw new ArgumentException("Max retries cannot be negative", nameof(maxRetries));

        // Create webhook delivery entity
        var webhookDelivery = new WebhookDelivery(
            id: Guid.NewGuid(),
            paymentId: paymentId,
            webhookUrl: webhookUrl,
            eventType: eventType,
            payload: payload,
            maxRetries: maxRetries);

        // Persist webhook delivery
        await _repository.AddAsync(webhookDelivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Webhook scheduled for delivery. PaymentId: {PaymentId}, EventType: {EventType}, WebhookUrl: {WebhookUrl}, MaxRetries: {MaxRetries}",
            paymentId, eventType, webhookUrl, maxRetries);

        // Attempt immediate delivery
        var result = await SendWebhookAsync(webhookUrl, eventType, payload, cancellationToken: cancellationToken);

        if (result.Success)
        {
            webhookDelivery.MarkAsDelivered();
        }
        else
        {
            webhookDelivery.MarkAsFailed(result.ErrorMessage ?? "Unknown error", result.HttpStatusCode);
        }

        await _repository.UpdateAsync(webhookDelivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return webhookDelivery.Id;
    }
}

