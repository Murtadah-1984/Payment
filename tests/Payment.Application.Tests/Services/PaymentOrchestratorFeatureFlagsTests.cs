using FluentAssertions;
using Moq;
using Microsoft.FeatureManagement;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Application.Tests.Services;

/// <summary>
/// Comprehensive tests for Feature Flags in PaymentOrchestrator (Feature Flags #17).
/// Tests SplitPayments feature flag behavior.
/// </summary>
public class PaymentOrchestratorFeatureFlagsTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly Mock<ISplitPaymentService> _splitPaymentServiceMock;
    private readonly Mock<IMetadataEnrichmentService> _metadataEnrichmentServiceMock;
    private readonly Mock<IIdempotencyService> _idempotencyServiceMock;
    private readonly Mock<IRequestHashService> _requestHashServiceMock;
    private readonly Mock<IPaymentFactory> _paymentFactoryMock;
    private readonly Mock<IPaymentProcessingService> _paymentProcessingServiceMock;
    private readonly Mock<IPaymentStatusUpdater> _paymentStatusUpdaterMock;
    private readonly Mock<IPaymentStateService> _stateServiceMock;
    private readonly Mock<IFeatureManager> _featureManagerMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<PaymentOrchestrator>> _loggerMock;
    private readonly PaymentOrchestrator _orchestrator;

    public PaymentOrchestratorFeatureFlagsTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _providerFactoryMock = new Mock<IPaymentProviderFactory>();
        _splitPaymentServiceMock = new Mock<ISplitPaymentService>();
        _metadataEnrichmentServiceMock = new Mock<IMetadataEnrichmentService>();
        _idempotencyServiceMock = new Mock<IIdempotencyService>();
        _requestHashServiceMock = new Mock<IRequestHashService>();
        _paymentFactoryMock = new Mock<IPaymentFactory>();
        _paymentProcessingServiceMock = new Mock<IPaymentProcessingService>();
        _paymentStatusUpdaterMock = new Mock<IPaymentStatusUpdater>();
        _stateServiceMock = new Mock<IPaymentStateService>();
        _featureManagerMock = new Mock<IFeatureManager>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<PaymentOrchestrator>>();

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
            _loggerMock.Object,
            new Mock<IMetricsRecorder>().Object,
            new Mock<IRegulatoryRulesEngine>().Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldThrowException_WhenSplitPaymentsDisabled_AndSplitRuleProvided()
    {
        // Arrange
        var request = CreatePaymentRequestWithSplitRule();
        _featureManagerMock.Setup(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        SetupMocksForIdempotencyCheck();

        // Act & Assert
        var act = async () => await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Split payments feature is currently disabled*");
        
        _featureManagerMock.Verify(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldThrowException_WhenSplitPaymentsDisabled_AndSystemFeePercentProvided()
    {
        // Arrange
        var request = CreatePaymentRequestWithSystemFee();
        _featureManagerMock.Setup(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        SetupMocksForIdempotencyCheck();

        // Act & Assert
        var act = async () => await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Split payments feature is currently disabled*");
        
        _featureManagerMock.Verify(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldProcessSplitPayment_WhenSplitPaymentsEnabled_AndSplitRuleProvided()
    {
        // Arrange
        var request = CreatePaymentRequestWithSplitRule();
        _featureManagerMock.Setup(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        SetupMocksForIdempotencyCheck();
        SetupMocksForSuccessfulPayment();

        var expectedSplit = new SplitPayment(10m, 90m, 10m);
        _splitPaymentServiceMock.Setup(s => s.CalculateMultiAccountSplit(
                It.IsAny<decimal>(), 
                It.IsAny<SplitRuleDto>()))
            .Returns((expectedSplit, new Dictionary<string, object>()));

        // Act
        var result = await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _featureManagerMock.Verify(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()), Times.Once);
        _splitPaymentServiceMock.Verify(s => s.CalculateMultiAccountSplit(
            It.IsAny<decimal>(), 
            It.IsAny<SplitRuleDto>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldProcessSplitPayment_WhenSplitPaymentsEnabled_AndSystemFeePercentProvided()
    {
        // Arrange
        var request = CreatePaymentRequestWithSystemFee();
        _featureManagerMock.Setup(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        SetupMocksForIdempotencyCheck();
        SetupMocksForSuccessfulPayment();

        var expectedSplit = new SplitPayment(10m, 90m, 10m);
        _splitPaymentServiceMock.Setup(s => s.CalculateSplit(It.IsAny<decimal>(), It.IsAny<decimal>()))
            .Returns(expectedSplit);

        // Act
        var result = await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _featureManagerMock.Verify(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()), Times.Once);
        _splitPaymentServiceMock.Verify(s => s.CalculateSplit(It.IsAny<decimal>(), It.IsAny<decimal>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotCalculateSplit_WhenSplitPaymentsEnabled_ButNoSplitRequested()
    {
        // Arrange
        var request = CreatePaymentRequestWithoutSplit();
        _featureManagerMock.Setup(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        SetupMocksForIdempotencyCheck();
        SetupMocksForSuccessfulPayment();

        // Act
        var result = await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _featureManagerMock.Verify(f => f.IsEnabledAsync("SplitPayments", It.IsAny<CancellationToken>()), Times.Once);
        _splitPaymentServiceMock.Verify(s => s.CalculateSplit(It.IsAny<decimal>(), It.IsAny<decimal>()), Times.Never);
        _splitPaymentServiceMock.Verify(s => s.CalculateMultiAccountSplit(It.IsAny<decimal>(), It.IsAny<SplitRuleDto>()), Times.Never);
    }

    private void SetupMocksForIdempotencyCheck()
    {
        _unitOfWorkMock.Setup(u => u.IdempotentRequests.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.IdempotentRequest?)null);
        _metadataEnrichmentServiceMock.Setup(m => m.EnrichMetadata(It.IsAny<CreatePaymentDto>(), It.IsAny<Dictionary<string, string>?>()))
            .Returns(new Dictionary<string, string>());
        _requestHashServiceMock.Setup(r => r.ComputeRequestHash(It.IsAny<CreatePaymentDto>()))
            .Returns("test-hash");
    }

    private void SetupMocksForSuccessfulPayment()
    {
        var payment = CreateMockPayment();
        _paymentFactoryMock.Setup(f => f.CreatePayment(It.IsAny<CreatePaymentDto>(), It.IsAny<SplitPayment?>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);
        
        _unitOfWorkMock.Setup(u => u.Payments.AddAsync(It.IsAny<Domain.Entities.Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Payment p, CancellationToken ct) => p);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        var mockProvider = new Mock<IPaymentProvider>();
        mockProvider.Setup(p => p.ProviderName).Returns("ZainCash");
        _providerFactoryMock.Setup(f => f.CreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockProvider.Object);
        
        _paymentProcessingServiceMock.Setup(p => p.ProcessPaymentAsync(
                It.IsAny<Domain.Entities.Payment>(), 
                It.IsAny<IPaymentProvider>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(Success: true, TransactionId: "txn-123", FailureReason: null, ProviderMetadata: null));
        
        _unitOfWorkMock.Setup(u => u.Payments.UpdateAsync(It.IsAny<Domain.Entities.Payment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.IdempotentRequests.AddAsync(It.IsAny<Domain.Entities.IdempotentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.IdempotentRequest r, CancellationToken ct) => r);
    }

    private static CreatePaymentDto CreatePaymentRequestWithSplitRule()
    {
        return new CreatePaymentDto(
            Guid.NewGuid(),
            100m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123",
            null,
            new SplitRuleDto(
                SystemFeePercent: 10m,
                Accounts: new List<SplitAccountDto>
                {
                    new SplitAccountDto(AccountType: "SystemOwner", AccountIdentifier: "sys-1", Percentage: 10m),
                    new SplitAccountDto(AccountType: "Owner", AccountIdentifier: "owner-1", Percentage: 90m)
                }),
            null,
            null,
            null,
            null);
    }

    private static CreatePaymentDto CreatePaymentRequestWithSystemFee()
    {
        return new CreatePaymentDto(
            Guid.NewGuid(),
            100m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123",
            10m,
            null,
            null,
            null,
            null,
            null);
    }

    private static CreatePaymentDto CreatePaymentRequestWithoutSplit()
    {
        return new CreatePaymentDto(
            Guid.NewGuid(),
            100m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-123",
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static Domain.Entities.Payment CreateMockPayment()
    {
        return new Domain.Entities.Payment(
            new PaymentId(Guid.NewGuid()),
            new Amount(100m),
            new Currency("USD"),
            new PaymentMethod("CreditCard"),
            new PaymentProvider("ZainCash"),
            "merchant-123",
            "order-456");
    }
}

