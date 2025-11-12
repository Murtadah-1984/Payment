using FluentAssertions;
using Moq;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.Logging;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;
using PaymentResult = Payment.Domain.Interfaces.PaymentResult;

namespace Payment.Application.Tests.Services;

public class PaymentOrchestratorTests
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

    public PaymentOrchestratorTests()
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

        _idempotencyServiceMock.Setup(i => i.CheckExistingPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        _requestHashServiceMock.Setup(h => h.ComputeRequestHash(It.IsAny<CreatePaymentDto>()))
            .Returns("test-hash-12345");

        idempotentRequestRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotentRequest?)null);

        _stateServiceMock.Setup(s => s.Transition(It.IsAny<Payment.Domain.Enums.PaymentStatus>(), It.IsAny<Payment.Domain.Enums.PaymentTrigger>()))
            .Returns<Payment.Domain.Enums.PaymentStatus, Payment.Domain.Enums.PaymentTrigger>((status, trigger) =>
            {
                return trigger switch
                {
                    Payment.Domain.Enums.PaymentTrigger.Process => Payment.Domain.Enums.PaymentStatus.Processing,
                    Payment.Domain.Enums.PaymentTrigger.Complete => Payment.Domain.Enums.PaymentStatus.Succeeded,
                    Payment.Domain.Enums.PaymentTrigger.Fail => Payment.Domain.Enums.PaymentStatus.Failed,
                    _ => status
                };
            });

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
    public async Task ProcessPaymentAsync_ShouldCreatePayment_WhenValidRequest()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var request = new CreatePaymentDto(
            requestId,
            100.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345");

        var paymentId = PaymentId.NewId();
        var payment = new PaymentEntity(
            paymentId,
            Amount.FromDecimal(100.00m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");

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
        result.Amount.Should().Be(100.00m);
        paymentRepositoryMock.Verify(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldCalculateSplitPayment_WhenSystemFeeProvided()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var request = new CreatePaymentDto(
            requestId,
            100.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345",
            SystemFeePercent: 5.0m);

        var splitPayment = new SplitPayment(5.00m, 95.00m, 5.0m);
        var paymentId = PaymentId.NewId();
        var payment = new PaymentEntity(
            paymentId,
            Amount.FromDecimal(100.00m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456",
            splitPayment);

        var paymentRepositoryMock = Mock.Get(_unitOfWorkMock.Object.Payments);
        paymentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity p, CancellationToken ct) => p);
        paymentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _splitPaymentServiceMock.Setup(s => s.CalculateSplit(100.00m, 5.0m))
            .Returns(splitPayment);

        _paymentFactoryMock.Setup(f => f.CreatePayment(It.IsAny<CreatePaymentDto>(), It.IsAny<SplitPayment?>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        var paymentResult = new PaymentResult(true, "TXN-123", null, null);
        _paymentProcessingServiceMock.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentEntity>(), It.IsAny<IPaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentResult);

        // Act
        var result = await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SplitPayment.Should().NotBeNull();
        result.SplitPayment!.SystemShare.Should().Be(5.00m);
        result.SplitPayment.OwnerShare.Should().Be(95.00m);
        result.SplitPayment.SystemFeePercent.Should().Be(5.0m);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnExistingPayment_WhenOrderIdExists()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var request = new CreatePaymentDto(
            requestId,
            100.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345");

        var existingPayment = new PaymentEntity(
            PaymentId.NewId(),
            Amount.FromDecimal(100.00m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.ZainCash,
            "merchant-123",
            "order-456");

        _idempotencyServiceMock.Setup(i => i.CheckExistingPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPayment);

        // Act
        var result = await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be("order-456");
        var paymentRepositoryMock = Mock.Get(_unitOfWorkMock.Object.Payments);
        paymentRepositoryMock.Verify(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

