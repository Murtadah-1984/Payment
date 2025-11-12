using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Providers;
using Xunit;

namespace Payment.Infrastructure.Tests.Providers;

/// <summary>
/// Comprehensive unit tests for StripePaymentProvider 3D Secure functionality.
/// Tests cover initiation, completion, requirement checking, and error handling.
/// </summary>
public class StripePaymentProviderThreeDSecureTests : IDisposable
{
    private readonly Mock<ILogger<StripePaymentProvider>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly HttpClient _httpClient;
    private readonly StripePaymentProvider _provider;
    private readonly TestHttpMessageHandler _messageHandler;

    public StripePaymentProviderThreeDSecureTests()
    {
        _loggerMock = new Mock<ILogger<StripePaymentProvider>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _messageHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_messageHandler);

        // Setup configuration
        _configurationMock.Setup(c => c["PaymentProviders:Stripe:BaseUrl"])
            .Returns("https://api.stripe.com/v1/");
        _configurationMock.Setup(c => c["PaymentProviders:Stripe:ApiKey"])
            .Returns("sk_test_1234567890");
        _configurationMock.Setup(c => c["PaymentProviders:Stripe:PublishableKey"])
            .Returns("pk_test_1234567890");
        _configurationMock.Setup(c => c.GetValue<bool>("PaymentProviders:Stripe:IsTestMode", true))
            .Returns(true);

        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(StripePaymentProvider)))
            .Returns(_httpClient);

        _provider = new StripePaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public void SupportsThreeDSecure_ShouldReturnTrue()
    {
        // Act
        var supports = ((IPaymentProvider)_provider).SupportsThreeDSecure();

        // Assert
        supports.Should().BeTrue();
    }

    [Fact]
    public void Provider_ShouldImplementIThreeDSecurePaymentProvider()
    {
        // Assert
        _provider.Should().BeAssignableTo<IThreeDSecurePaymentProvider>();
    }

    [Fact]
    public async Task InitiateThreeDSecureAsync_ShouldReturnChallenge_When3DSRequired()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(100.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            new Dictionary<string, string>
            {
                { "payment_method_id", "pm_1234567890" }
            });

        var returnUrl = "https://example.com/return";

        var paymentIntentResponse = new
        {
            id = "pi_1234567890",
            status = "requires_action",
            client_secret = "pi_1234567890_secret_xyz",
            next_action = new
            {
                type = "redirect_to_url",
                redirect_to_url = new
                {
                    url = "https://acs.stripe.com/authenticate",
                    return_url = returnUrl
                }
            }
        };

        _messageHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(paymentIntentResponse));

        // Act
        var result = await _provider.InitiateThreeDSecureAsync(request, returnUrl, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.AcsUrl.Should().Be("https://acs.stripe.com/authenticate");
        result.Md.Should().Be("pi_1234567890");
        result.TermUrl.Should().Be(returnUrl);
        result.Version.Should().Be("2.2.0");
        result.Pareq.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InitiateThreeDSecureAsync_ShouldReturnNull_WhenPaymentMethodNotProvided()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(100.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            null); // No payment_method_id

        var returnUrl = "https://example.com/return";

        // Act
        var result = await _provider.InitiateThreeDSecureAsync(request, returnUrl, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task InitiateThreeDSecureAsync_ShouldReturnNull_When3DSNotRequired()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(100.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            new Dictionary<string, string>
            {
                { "payment_method_id", "pm_1234567890" }
            });

        var returnUrl = "https://example.com/return";

        var paymentIntentResponse = new
        {
            id = "pi_1234567890",
            status = "succeeded", // Already succeeded, no 3DS needed
            client_secret = "pi_1234567890_secret_xyz"
        };

        _messageHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(paymentIntentResponse));

        // Act
        var result = await _provider.InitiateThreeDSecureAsync(request, returnUrl, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CompleteThreeDSecureAsync_ShouldReturnSuccess_WhenPaymentIntentSucceeded()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(100.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            null);

        var paymentIntentId = "pi_1234567890";
        var pareq = "base64-pareq";
        var ares = "base64-ares";
        var md = paymentIntentId;

        var paymentIntentResponse = new
        {
            id = paymentIntentId,
            status = "succeeded",
            latest_charge = "ch_1234567890",
            metadata = new Dictionary<string, string>
            {
                { "3ds_cavv", "cavv-123" },
                { "3ds_eci", "05" },
                { "3ds_xid", "xid-123" }
            }
        };

        _messageHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(paymentIntentResponse));

        // Act
        var result = await _provider.CompleteThreeDSecureAsync(request, pareq, ares, md, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ThreeDSecureResult.Authenticated.Should().BeTrue();
        result.ThreeDSecureResult.Cavv.Should().Be("cavv-123");
        result.ThreeDSecureResult.Eci.Should().Be("05");
        result.ThreeDSecureResult.Xid.Should().Be("xid-123");
        result.PaymentResult.Success.Should().BeTrue();
        result.PaymentResult.TransactionId.Should().Be(paymentIntentId);
    }

    [Fact]
    public async Task CompleteThreeDSecureAsync_ShouldConfirmPaymentIntent_WhenRequiresAction()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(100.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            new Dictionary<string, string>
            {
                { "return_url", "https://example.com/return" }
            });

        var paymentIntentId = "pi_1234567890";
        var pareq = "base64-pareq";
        var ares = "base64-ares";
        var md = paymentIntentId;

        // First call: Get Payment Intent (requires_action)
        var getResponse = new
        {
            id = paymentIntentId,
            status = "requires_action"
        };

        // Second call: Confirm Payment Intent (succeeded)
        var confirmResponse = new
        {
            id = paymentIntentId,
            status = "succeeded",
            latest_charge = "ch_1234567890"
        };

        _messageHandler.SetResponses(
            new[] { HttpStatusCode.OK, HttpStatusCode.OK },
            new[] { JsonSerializer.Serialize(getResponse), JsonSerializer.Serialize(confirmResponse) });

        // Act
        var result = await _provider.CompleteThreeDSecureAsync(request, pareq, ares, md, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ThreeDSecureResult.Authenticated.Should().BeTrue();
        result.PaymentResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteThreeDSecureAsync_ShouldReturnFailure_WhenPaymentIntentNotFound()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(100.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            null);

        var paymentIntentId = "pi_1234567890";
        var pareq = "base64-pareq";
        var ares = "base64-ares";
        var md = paymentIntentId;

        _messageHandler.SetResponse(HttpStatusCode.NotFound, "{}");

        // Act
        var result = await _provider.CompleteThreeDSecureAsync(request, pareq, ares, md, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ThreeDSecureResult.Authenticated.Should().BeFalse();
        result.ThreeDSecureResult.FailureReason.Should().Contain("not found");
        result.PaymentResult.Success.Should().BeFalse();
    }

    [Fact]
    public async Task IsThreeDSecureRequiredAsync_ShouldReturnTrue_WhenEURCurrency()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(50.00m),
            Currency.FromCode("EUR"),
            "merchant-123",
            "order-456",
            null,
            new Dictionary<string, string>
            {
                { "payment_method_id", "pm_1234567890" }
            });

        // Act
        var result = await _provider.IsThreeDSecureRequiredAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeTrue(); // EUR requires 3DS for any amount (SCA)
    }

    [Fact]
    public async Task IsThreeDSecureRequiredAsync_ShouldReturnTrue_WhenAmountAboveThreshold()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(150.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            new Dictionary<string, string>
            {
                { "payment_method_id", "pm_1234567890" }
            });

        // Act
        var result = await _provider.IsThreeDSecureRequiredAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeTrue(); // Above $100 threshold
    }

    [Fact]
    public async Task IsThreeDSecureRequiredAsync_ShouldReturnFalse_WhenPaymentMethodMissing()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(150.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            null); // No payment_method_id

        // Act
        var result = await _provider.IsThreeDSecureRequiredAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsThreeDSecureRequiredAsync_ShouldReturnFalse_WhenAmountBelowThreshold()
    {
        // Arrange
        var request = new PaymentRequest(
            Amount.FromDecimal(50.00m),
            Currency.USD,
            "merchant-123",
            "order-456",
            null,
            new Dictionary<string, string>
            {
                { "payment_method_id", "pm_1234567890" }
            });

        // Act
        var result = await _provider.IsThreeDSecureRequiredAsync(request, CancellationToken.None);

        // Assert
        result.Should().BeFalse(); // Below $100 threshold for non-EUR
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _messageHandler?.Dispose();
    }
}

/// <summary>
/// Test HTTP message handler for mocking HTTP responses.
/// </summary>
public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode statusCode, string content)> _responses = new();

    public void SetResponse(HttpStatusCode statusCode, string content)
    {
        _responses.Clear();
        _responses.Enqueue((statusCode, content));
    }

    public void SetResponses(HttpStatusCode[] statusCodes, string[] contents)
    {
        _responses.Clear();
        for (int i = 0; i < statusCodes.Length && i < contents.Length; i++)
        {
            _responses.Enqueue((statusCodes[i], contents[i]));
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_responses.Count == 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        var (statusCode, content) = _responses.Dequeue();
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}

