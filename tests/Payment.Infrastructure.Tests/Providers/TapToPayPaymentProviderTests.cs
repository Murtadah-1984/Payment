using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Caching;
using Payment.Infrastructure.Providers;
using Xunit;

namespace Payment.Infrastructure.Tests.Providers;

/// <summary>
/// Comprehensive unit tests for TapToPayPaymentProvider.
/// Tests cover successful payments, failures, replay prevention, metrics, and error handling.
/// </summary>
public class TapToPayPaymentProviderTests : IDisposable
{
    private readonly Mock<ILogger<TapToPayPaymentProvider>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly HttpClient _httpClient;
    private readonly TapToPayPaymentProvider _provider;
    private readonly Type _tokenMarkerType;

    public TapToPayPaymentProviderTests()
    {
        _loggerMock = new Mock<ILogger<TapToPayPaymentProvider>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _cacheServiceMock = new Mock<ICacheService>();

        // Setup configuration
        _configurationMock.Setup(c => c["PaymentProviders:TapToPay:BaseUrl"])
            .Returns("https://api.tap.company/v2/");
        _configurationMock.Setup(c => c["PaymentProviders:TapToPay:SecretKey"])
            .Returns("sk_test_1234567890");
        _configurationMock.Setup(c => c["PaymentProviders:TapToPay:PublishableKey"])
            .Returns("pk_test_1234567890");
        _configurationMock.Setup(c => c.GetValue<bool>("PaymentProviders:TapToPay:IsTestMode", true))
            .Returns(true);
        _configurationMock.Setup(c => c.GetValue<bool>("PaymentProviders:TapToPay:ReplayPreventionEnabled", true))
            .Returns(true);

        // Setup HTTP client
        _httpClient = new HttpClient(new TestHttpMessageHandler());
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(_httpClient);

        // Get the private TokenMarker type using reflection for verification
        _tokenMarkerType = typeof(TapToPayPaymentProvider)
            .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(t => t.Name == "TokenMarker") 
            ?? throw new InvalidOperationException("TokenMarker type not found");

        // Setup cache service using reflection to match the private TokenMarker type
        // This allows us to mock the cache service calls that use the private type
        var setupCacheServiceMethod = typeof(TapToPayPaymentProviderTests)
            .GetMethod(nameof(SetupCacheServiceMock), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var genericSetupMethod = setupCacheServiceMethod.MakeGenericMethod(_tokenMarkerType);
        genericSetupMethod.Invoke(this, new object[] { _cacheServiceMock });

        _provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);
    }

    [Fact]
    public void ProviderName_ShouldReturnTapToPay()
    {
        // Assert
        _provider.ProviderName.Should().Be("TapToPay");
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnFailure_WhenSecretKeyNotConfigured()
    {
        // Arrange
        _configurationMock.Setup(c => c["PaymentProviders:TapToPay:SecretKey"])
            .Returns(string.Empty);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        var request = CreateValidPaymentRequest();

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("SecretKey must be configured");
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnFailure_WhenNfcTokenMissing()
    {
        // Arrange
        var request = CreatePaymentRequestWithoutNfcToken();

        // Act
        var result = await _provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("NFC token is required");
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NFC token is required")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnFailure_WhenNfcTokenEmpty()
    {
        // Arrange
        var request = CreatePaymentRequestWithEmptyNfcToken();

        // Act
        var result = await _provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("NFC token is required");
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnFailure_WhenReplayDetected()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken);

        // Setup cache to return existing token (replay detected)
        // Use reflection to create an instance of the private TokenMarker type
        var cachedToken = Activator.CreateInstance(_tokenMarkerType);
        _tokenMarkerType.GetProperty("Hash")!.SetValue(cachedToken, "test-hash");
        
        // Set up mock using reflection for the actual TokenMarker type
        var getAsyncMethod = typeof(ICacheService).GetMethod("GetAsync")!;
        var genericGetAsync = getAsyncMethod.MakeGenericMethod(_tokenMarkerType);
        var setupMethod = typeof(Mock<>).MakeGenericType(typeof(ICacheService))
            .GetMethod("Setup", new[] { typeof(Expression<Func<ICacheService, object>>) })!;
        
        // Use a simpler approach: set up the mock to return the cached token for any key containing "tap_to_pay_token:"
        var setupCacheForReplayMethod = typeof(TapToPayPaymentProviderTests)
            .GetMethod(nameof(SetupCacheServiceMockForReplay), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var genericSetupReplayMethod = setupCacheForReplayMethod.MakeGenericMethod(_tokenMarkerType);
        genericSetupReplayMethod.Invoke(this, new object[] { _cacheServiceMock, cachedToken });

        // Act
        var result = await _provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("already been processed");
        result.ProviderMetadata.Should().ContainKey("ReplayDetected");
        result.ProviderMetadata!["ReplayDetected"].Should().Be("true");
        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Duplicate NFC token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldCacheToken_WhenReplayPreventionEnabled()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken);

        // Setup HTTP response for successful payment
        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateSuccessfulTapResponse(), Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        // Verify that SetAsync was called with a key containing "tap_to_pay_token:" and 24-hour TTL
        // We verify the call was made without checking the exact generic type (which is private)
        _cacheServiceMock.Verify(
            c => c.SetAsync(
                It.Is<string>(k => k.Contains("tap_to_pay_token:")),
                It.IsAny<It.IsAnyType>(),
                It.Is<TimeSpan>(t => t.TotalHours == 24),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotCheckCache_WhenReplayPreventionDisabled()
    {
        // Arrange
        _configurationMock.Setup(c => c.GetValue<bool>("PaymentProviders:TapToPay:ReplayPreventionEnabled", true))
            .Returns(false);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken);

        // Setup HTTP response for successful payment
        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateSuccessfulTapResponse(), Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        // Verify that GetAsync was not called (replay prevention disabled)
        // We can't verify the exact type since it's private, but we can verify the method wasn't called
        _cacheServiceMock.Invocations.Should().NotContain(i => 
            i.Method.Name == "GetAsync" && 
            i.Arguments.Any(arg => arg != null && arg.GetType() == typeof(string) && ((string)arg).Contains("tap_to_pay_token:")));
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnSuccess_WhenPaymentSucceeds()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken);

        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateSuccessfulTapResponse(), Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.TransactionId.Should().Be("chg_test_1234567890");
        result.ProviderMetadata.Should().NotBeNull();
        result.ProviderMetadata!["Provider"].Should().Be("TapToPay");
        result.ProviderMetadata["Status"].Should().Be("CAPTURED");
        result.ProviderMetadata["ChargeId"].Should().Be("chg_test_1234567890");
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnFailure_WhenApiReturnsError()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken);

        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(CreateErrorTapResponse(), Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("Invalid token");
        result.ProviderMetadata.Should().NotBeNull();
        result.ProviderMetadata!["StatusCode"].Should().Be("BadRequest");
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldHandlePendingStatus()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken);

        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreatePendingTapResponse(), Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse(); // Pending status is not considered success
        result.ProviderMetadata!["Status"].Should().Be("PENDING");
        result.ProviderMetadata["RequiresAction"].Should().Be("True");
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldIncludeDeviceId_WhenProvided()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var deviceId = "device-xyz-123";
        var request = CreatePaymentRequestWithNfcToken(nfcToken, deviceId: deviceId);

        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateSuccessfulTapResponse(), Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ProviderMetadata!["DeviceId"].Should().Be(deviceId);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldHandleSplitPayments()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken);
        request.Metadata!["destination_account_id"] = "acc_1234567890";

        var splitPayment = new SplitPayment(
            5.00m,
            95.00m,
            5.0m);
        request = request with { SplitPayment = splitPayment };

        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateSuccessfulTapResponse(), Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("split payment")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldHandleHttpException()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken);

        var httpHandler = new TestHttpMessageHandler
        {
            ShouldThrow = true,
            Exception = new HttpRequestException("Network error")
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        var result = await provider.ProcessPaymentAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("Network error");
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing Tap-to-Pay")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldConvertAmountToSmallestCurrencyUnit()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken, amount: 100.50m, currency: "USD");

        var capturedRequest = new CapturedHttpRequest();
        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateSuccessfulTapResponse(), Encoding.UTF8, "application/json")
            },
            OnRequest = (req) => capturedRequest.Request = req
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        await provider.ProcessPaymentAsync(request);

        // Assert
        capturedRequest.Request.Should().NotBeNull();
        var requestBody = await capturedRequest.Request!.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(requestBody);
        json.GetProperty("amount").GetInt64().Should().Be(10050); // 100.50 * 100
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldHandleCurrenciesWithoutSubunits()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = CreatePaymentRequestWithNfcToken(nfcToken, amount: 1000m, currency: "JPY");

        var capturedRequest = new CapturedHttpRequest();
        var httpHandler = new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateSuccessfulTapResponse(), Encoding.UTF8, "application/json")
            },
            OnRequest = (req) => capturedRequest.Request = req
        };
        var httpClient = new HttpClient(httpHandler);
        _httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TapToPayPaymentProvider)))
            .Returns(httpClient);

        var provider = new TapToPayPaymentProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _cacheServiceMock.Object);

        // Act
        await provider.ProcessPaymentAsync(request);

        // Assert
        capturedRequest.Request.Should().NotBeNull();
        var requestBody = await capturedRequest.Request!.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(requestBody);
        json.GetProperty("amount").GetInt64().Should().Be(1000); // JPY has no subunits
    }

    // Helper methods
    private PaymentRequest CreateValidPaymentRequest()
    {
        return CreatePaymentRequestWithNfcToken("eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token");
    }

    private PaymentRequest CreatePaymentRequestWithNfcToken(
        string nfcToken,
        decimal amount = 100.00m,
        string currency = "IQD",
        string? deviceId = null,
        string? customerId = null)
    {
        var metadata = new Dictionary<string, string>
        {
            { "nfc_token", nfcToken }
        };

        if (!string.IsNullOrEmpty(deviceId))
        {
            metadata["device_id"] = deviceId;
        }

        if (!string.IsNullOrEmpty(customerId))
        {
            metadata["customer_id"] = customerId;
        }

        return new PaymentRequest(
            Amount.FromDecimal(amount),
            Currency.FromCode(currency),
            "MRC-001",
            "ORD-10001",
            null,
            metadata);
    }

    private PaymentRequest CreatePaymentRequestWithoutNfcToken()
    {
        return new PaymentRequest(
            Amount.FromDecimal(100.00m),
            Currency.FromCode("IQD"),
            "MRC-001",
            "ORD-10001",
            null,
            new Dictionary<string, string>());
    }

    private PaymentRequest CreatePaymentRequestWithEmptyNfcToken()
    {
        var metadata = new Dictionary<string, string>
        {
            { "nfc_token", string.Empty }
        };

        return new PaymentRequest(
            Amount.FromDecimal(100.00m),
            Currency.FromCode("IQD"),
            "MRC-001",
            "ORD-10001",
            null,
            metadata);
    }

    private string CreateSuccessfulTapResponse()
    {
        return JsonSerializer.Serialize(new
        {
            charge = new
            {
                id = "chg_test_1234567890",
                status = "CAPTURED",
                amount = 10000,
                currency = "IQD",
                reference = new
                {
                    transaction = "ORD-10001",
                    order = "ORD-10001"
                }
            }
        });
    }

    private string CreatePendingTapResponse()
    {
        return JsonSerializer.Serialize(new
        {
            charge = new
            {
                id = "chg_test_1234567890",
                status = "PENDING",
                amount = 10000,
                currency = "IQD",
                redirect = new
                {
                    url = "https://tap.company/redirect"
                }
            }
        });
    }

    private string CreateErrorTapResponse()
    {
        return JsonSerializer.Serialize(new
        {
            errors = new[]
            {
                new
                {
                    code = "INVALID_TOKEN",
                    message = "Invalid token provided"
                }
            }
        });
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    // Helper classes for testing
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; set; }
        public bool ShouldThrow { get; set; }
        public Exception? Exception { get; set; }
        public Action<HttpRequestMessage>? OnRequest { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            OnRequest?.Invoke(request);

            if (ShouldThrow && Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private class CapturedHttpRequest
    {
        public HttpRequestMessage? Request { get; set; }
    }

    // Helper method to set up cache service mock for any type T
    private void SetupCacheServiceMock<T>(Mock<ICacheService> cacheServiceMock) where T : class
    {
        cacheServiceMock.Setup(c => c.GetAsync<T>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((T?)null);
    }

    // Helper method to set up cache service mock for replay detection
    private void SetupCacheServiceMockForReplay<T>(Mock<ICacheService> cacheServiceMock, T? cachedValue) where T : class
    {
        cacheServiceMock.Setup(c => c.GetAsync<T>(
                It.Is<string>(k => k.Contains("tap_to_pay_token:")), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue);
    }

    // Test helper class matching the private TokenMarker in TapToPayPaymentProvider
    private class TestTokenMarker
    {
        public string Hash { get; set; } = string.Empty;
    }
}

