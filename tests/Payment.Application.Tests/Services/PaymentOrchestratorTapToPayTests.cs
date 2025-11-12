using FluentAssertions;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;
using PaymentResult = Payment.Domain.Interfaces.PaymentResult;

namespace Payment.Application.Tests.Services;

/// <summary>
/// Integration tests for PaymentOrchestrator with Tap-to-Pay provider.
/// Tests the orchestrator's handling of Tap-to-Pay specific fields.
/// </summary>
public class PaymentOrchestratorTapToPayTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly Mock<IPaymentProvider> _providerMock;
    private readonly Mock<ISplitPaymentService> _splitPaymentServiceMock;
    private readonly Mock<IMetadataEnrichmentService> _metadataEnrichmentServiceMock;
    private readonly Mock<IIdempotencyService> _idempotencyServiceMock;
    private readonly Mock<IRequestHashService> _requestHashServiceMock;
    private readonly Mock<IPaymentFactory> _paymentFactoryMock;
    private readonly Mock<IPaymentProcessingService> _paymentProcessingServiceMock;
    private readonly Mock<IPaymentStatusUpdater> _paymentStatusUpdaterMock;
    private readonly Mock<IPaymentStateService> _stateServiceMock;
    private readonly Mock<Microsoft.FeatureManagement.IFeatureManager> _featureManagerMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<PaymentOrchestrator>> _loggerMock;
    private readonly PaymentOrchestrator _orchestrator;

    public PaymentOrchestratorTapToPayTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _providerFactoryMock = new Mock<IPaymentProviderFactory>();
        _providerMock = new Mock<IPaymentProvider>();
        _splitPaymentServiceMock = new Mock<ISplitPaymentService>();
        _metadataEnrichmentServiceMock = new Mock<IMetadataEnrichmentService>();
        _idempotencyServiceMock = new Mock<IIdempotencyService>();
        _requestHashServiceMock = new Mock<IRequestHashService>();
        _paymentFactoryMock = new Mock<IPaymentFactory>();
        _paymentProcessingServiceMock = new Mock<IPaymentProcessingService>();
        _paymentStatusUpdaterMock = new Mock<IPaymentStatusUpdater>();
        _stateServiceMock = new Mock<IPaymentStateService>();
        _featureManagerMock = new Mock<Microsoft.FeatureManagement.IFeatureManager>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<PaymentOrchestrator>>();

        // Setup default mocks
        _unitOfWorkMock.Setup(u => u.IdempotentRequests.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotentRequest?)null);
        _unitOfWorkMock.Setup(u => u.Payments).Returns(new Mock<IPaymentRepository>().Object);
        _metadataEnrichmentServiceMock.Setup(m => m.EnrichMetadata(It.IsAny<CreatePaymentDto>(), It.IsAny<Dictionary<string, string>?>()))
            .Returns<CreatePaymentDto, Dictionary<string, string>?>((dto, metadata) => metadata ?? new Dictionary<string, string>());
        _requestHashServiceMock.Setup(r => r.ComputeRequestHash(It.IsAny<CreatePaymentDto>()))
            .Returns("test-hash");
        _featureManagerMock.Setup(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _providerMock.Setup(p => p.ProviderName).Returns("TapToPay");
        _providerFactoryMock.Setup(f => f.CreateAsync("TapToPay", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_providerMock.Object);
        _paymentProcessingServiceMock.Setup(p => p.ProcessPaymentAsync(
                It.IsAny<PaymentEntity>(),
                It.IsAny<IPaymentProvider>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "chg_test_1234567890", null, null));
        _paymentStatusUpdaterMock.Setup(u => u.UpdateStatusAsync(
                It.IsAny<PaymentEntity>(),
                It.IsAny<PaymentResult>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _orchestrator = new PaymentOrchestrator(
            _unitOfWorkMock.Object,
            _providerFactoryMock.Object,
            _splitPaymentServiceMock.Object,
            _metadataEnrichmentServiceMock.Object,
            _idempotencyServiceMock.Object,
            _requestHashServiceMock.Object,
            _paymentFactoryMock.Object,
            _paymentProcessingServiceMock.Object,
            _paymentStatusUpdaterMock.Object,
            _stateServiceMock.Object,
            _featureManagerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldIncludeNfcTokenInMetadata_WhenProvided()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var request = new CreatePaymentDto(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "MRC-001",
            "ORD-10001",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            nfcToken,
            "device-123",
            "customer-456");

        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.00m),
            Currency.FromCode("IQD"),
            PaymentMethod.TapToPay,
            PaymentProvider.TapToPay,
            "MRC-001",
            "ORD-10001");

        _paymentFactoryMock.Setup(f => f.CreatePayment(
                It.IsAny<CreatePaymentDto>(),
                It.IsAny<SplitPayment?>(),
                It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        // Act
        await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        _paymentFactoryMock.Verify(f => f.CreatePayment(
            It.IsAny<CreatePaymentDto>(),
            It.IsAny<SplitPayment?>(),
            It.Is<Dictionary<string, string>>(m => 
                m.ContainsKey("nfc_token") &&
                m["nfc_token"] == nfcToken &&
                m.ContainsKey("device_id") &&
                m["device_id"] == "device-123" &&
                m.ContainsKey("customer_id") &&
                m["customer_id"] == "customer-456")),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotIncludeNfcTokenInMetadata_WhenNotProvided()
    {
        // Arrange
        var request = new CreatePaymentDto(
            Guid.NewGuid(),
            100.00m,
            "USD",
            "CreditCard",
            "Stripe",
            "MRC-001",
            "ORD-10001",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            null, // No NFC token
            null,
            null);

        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.00m),
            Currency.FromCode("USD"),
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "MRC-001",
            "ORD-10001");

        _paymentFactoryMock.Setup(f => f.CreatePayment(
                It.IsAny<CreatePaymentDto>(),
                It.IsAny<SplitPayment?>(),
                It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        // Act
        await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        _paymentFactoryMock.Verify(f => f.CreatePayment(
            It.IsAny<CreatePaymentDto>(),
            It.IsAny<SplitPayment?>(),
            It.Is<Dictionary<string, string>>(m => !m.ContainsKey("nfc_token"))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldUseTapToPayProvider_WhenProviderIsTapToPay()
    {
        // Arrange
        var request = new CreatePaymentDto(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "MRC-001",
            "ORD-10001",
            "PROJECT-001",
            "idempotency-key-12345678901234567890",
            null,
            null,
            null,
            null,
            null,
            null,
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token",
            null,
            null);

        var payment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.00m),
            Currency.FromCode("IQD"),
            PaymentMethod.TapToPay,
            PaymentProvider.TapToPay,
            "MRC-001",
            "ORD-10001");

        _paymentFactoryMock.Setup(f => f.CreatePayment(
                It.IsAny<CreatePaymentDto>(),
                It.IsAny<SplitPayment?>(),
                It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        // Act
        await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        _providerFactoryMock.Verify(f => f.CreateAsync("TapToPay", It.IsAny<CancellationToken>()), Times.Once);
        _paymentProcessingServiceMock.Verify(p => p.ProcessPaymentAsync(
            payment,
            _providerMock.Object,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

