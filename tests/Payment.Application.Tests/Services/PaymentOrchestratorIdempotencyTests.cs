using FluentAssertions;
using Moq;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Exceptions;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;
using PaymentResult = Payment.Domain.Interfaces.PaymentResult;

namespace Payment.Application.Tests.Services;

public class PaymentOrchestratorIdempotencyTests
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
    private readonly Mock<IFeatureManager> _featureManagerMock;
    private readonly Mock<ILogger<PaymentOrchestrator>> _loggerMock;
    private readonly Mock<IMetricsRecorder> _metricsRecorderMock;
    private readonly Mock<IRegulatoryRulesEngine> _regulatoryRulesEngineMock;
    private readonly PaymentOrchestrator _orchestrator;

    public PaymentOrchestratorIdempotencyTests()
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
        _featureManagerMock = new Mock<IFeatureManager>();
        _loggerMock = new Mock<ILogger<PaymentOrchestrator>>();
        _metricsRecorderMock = new Mock<IMetricsRecorder>();
        _regulatoryRulesEngineMock = new Mock<IRegulatoryRulesEngine>();

        var paymentRepositoryMock = new Mock<IPaymentRepository>();
        var idempotentRequestRepositoryMock = new Mock<IIdempotentRequestRepository>();
        _unitOfWorkMock.Setup(u => u.Payments).Returns(paymentRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.IdempotentRequests).Returns(idempotentRequestRepositoryMock.Object);

        _providerMock.Setup(p => p.ProviderName).Returns("ZainCash");
        _providerFactoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(_providerMock.Object);

        _metadataEnrichmentServiceMock.Setup(m => m.EnrichMetadata(It.IsAny<CreatePaymentDto>(), It.IsAny<Dictionary<string, string>?>()))
            .Returns<CreatePaymentDto, Dictionary<string, string>?>((dto, metadata) => metadata ?? new Dictionary<string, string>());

        _requestHashServiceMock.Setup(h => h.ComputeRequestHash(It.IsAny<CreatePaymentDto>()))
            .Returns("test-hash-12345");

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
            _metricsRecorderMock.Object,
            _regulatoryRulesEngineMock.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithExistingIdempotencyKey_ShouldReturnExistingPayment()
    {
        // Arrange
        var idempotencyKey = "existing-idempotency-key-12345";
        var request = new CreatePaymentDto(
            RequestId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard",
            Provider: "ZainCash",
            MerchantId: "merchant-123",
            OrderId: "order-456",
            ProjectCode: "PROJECT-001",
            IdempotencyKey: idempotencyKey);

        var existingPaymentId = PaymentId.NewId();
        var existingPayment = new PaymentEntity(
            existingPaymentId,
            Amount.FromDecimal(100.00m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");

        var existingIdempotentRequest = new IdempotentRequest(
            idempotencyKey,
            existingPaymentId.Value,
            "test-hash-12345",
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(23));

        var idempotentRequestRepositoryMock = Mock.Get(_unitOfWorkMock.Object.IdempotentRequests);
        idempotentRequestRepositoryMock.Setup(r => r.GetByKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIdempotentRequest);

        var paymentRepositoryMock = Mock.Get(_unitOfWorkMock.Object.Payments);
        paymentRepositoryMock.Setup(r => r.GetByIdAsync(existingPaymentId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPayment);

        // Act
        var result = await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(existingPaymentId.Value);
        paymentRepositoryMock.Verify(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithIdempotencyKeyMismatch_ShouldThrowException()
    {
        // Arrange
        var idempotencyKey = "existing-idempotency-key-12345";
        var request = new CreatePaymentDto(
            RequestId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard",
            Provider: "ZainCash",
            MerchantId: "merchant-123",
            OrderId: "order-456",
            ProjectCode: "PROJECT-001",
            IdempotencyKey: idempotencyKey);

        var existingIdempotentRequest = new IdempotentRequest(
            idempotencyKey,
            Guid.NewGuid(),
            "different-hash-67890", // Different hash
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(23));

        var idempotentRequestRepositoryMock = Mock.Get(_unitOfWorkMock.Object.IdempotentRequests);
        idempotentRequestRepositoryMock.Setup(r => r.GetByKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIdempotentRequest);

        _requestHashServiceMock.Setup(h => h.ComputeRequestHash(request))
            .Returns("test-hash-12345"); // Different from existing hash

        // Act & Assert
        await Assert.ThrowsAsync<IdempotencyKeyMismatchException>(
            () => _orchestrator.ProcessPaymentAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithNewIdempotencyKey_ShouldStoreIdempotencyRecord()
    {
        // Arrange
        var idempotencyKey = "new-idempotency-key-12345";
        var request = new CreatePaymentDto(
            RequestId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard",
            Provider: "ZainCash",
            MerchantId: "merchant-123",
            OrderId: "order-456",
            ProjectCode: "PROJECT-001",
            IdempotencyKey: idempotencyKey);

        var paymentId = PaymentId.NewId();
        var payment = new PaymentEntity(
            paymentId,
            Amount.FromDecimal(100.00m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");

        var idempotentRequestRepositoryMock = Mock.Get(_unitOfWorkMock.Object.IdempotentRequests);
        idempotentRequestRepositoryMock.Setup(r => r.GetByKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotentRequest?)null);
        idempotentRequestRepositoryMock.Setup(r => r.AddAsync(It.IsAny<IdempotentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotentRequest ir, CancellationToken ct) => ir);

        var paymentRepositoryMock = Mock.Get(_unitOfWorkMock.Object.Payments);
        paymentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity p, CancellationToken ct) => p);
        paymentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _paymentFactoryMock.Setup(f => f.CreatePayment(It.IsAny<CreatePaymentDto>(), It.IsAny<SplitPayment?>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        var paymentResult = new PaymentResult(true, "TXN-123", null, null);
        _paymentProcessingServiceMock.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentEntity>(), It.IsAny<IPaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentResult);

        // Act
        var result = await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        idempotentRequestRepositoryMock.Verify(
            r => r.AddAsync(
                It.Is<IdempotentRequest>(ir => ir.IdempotencyKey == idempotencyKey && ir.PaymentId == paymentId.Value),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}


