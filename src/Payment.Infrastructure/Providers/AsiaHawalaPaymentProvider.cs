using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Providers;

public class AsiaHawalaPaymentProvider : IPaymentProvider
{
    private readonly ILogger<AsiaHawalaPaymentProvider> _logger;
    private readonly HttpClient _httpClient;

    public string ProviderName => "AsiaHawala";

    public AsiaHawalaPaymentProvider(
        ILogger<AsiaHawalaPaymentProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(AsiaHawalaPaymentProvider));
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing payment with AsiaHawala for order {OrderId}, amount {Amount} {Currency}",
            request.OrderId, request.Amount.Value, request.Currency.Code);

        try
        {
            // TODO: Replace with actual AsiaHawala API integration
            // For now, this is a mock implementation
            
            // Simulate API call delay
            await Task.Delay(500, cancellationToken);

            // Mock successful payment processing
            var transactionId = $"AH-{Guid.NewGuid():N}";
            
            _logger.LogInformation("AsiaHawala payment successful. Transaction ID: {TransactionId}", transactionId);

            // If split payment is required, process both transactions
            if (request.SplitPayment != null)
            {
                _logger.LogInformation("Processing split payment: System={SystemShare}, Owner={OwnerShare}",
                    request.SplitPayment.SystemShare, request.SplitPayment.OwnerShare);
                
                // In real implementation, you would:
                // 1. Transfer system share to system account
                // 2. Transfer owner share to owner account
                // 3. Both transactions should succeed or both should fail (atomicity)
            }

            return new PaymentResult(
                Success: true,
                TransactionId: transactionId,
                FailureReason: null,
                ProviderMetadata: new Dictionary<string, string>
                {
                    { "Provider", "AsiaHawala" },
                    { "ProviderTransactionId", transactionId },
                    { "ProcessedAt", DateTime.UtcNow.ToString("O") }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AsiaHawala payment for order {OrderId}", request.OrderId);
            return new PaymentResult(
                Success: false,
                TransactionId: null,
                FailureReason: $"AsiaHawala payment failed: {ex.Message}",
                ProviderMetadata: null);
        }
    }
}

