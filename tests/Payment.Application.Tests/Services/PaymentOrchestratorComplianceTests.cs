using FluentAssertions;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Exceptions;
using Payment.Domain.Interfaces;
using Payment.Domain.Services;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;
using PaymentResult = Payment.Domain.Interfaces.PaymentResult;

namespace Payment.Application.Tests.Services;

public class PaymentOrchestratorComplianceTests
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
    private readonly Mock<IRegulatoryRulesEngine> _regulatoryRulesEngineMock;
    private readonly PaymentOrchestrator _orchestrator;

    public PaymentOrchestratorComplianceTests()
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
        _regulatoryRulesEngineMock = new Mock<IRegulatoryRulesEngine>();

        var paymentRepositoryMock = new Mock<IPaymentRepository>();
        var idempotentRequestRepositoryMock = new Mock<IIdempotentRequestRepository>();
        _unitOfWorkMock.Setup(u => u.Payments).Returns(paymentRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.IdempotentRequests).Returns(idempotentRequestRepositoryMock.Object);

        _providerMock.Setup(p => p.ProviderName).Returns("ZainCash");
        _providerFactoryMock.Setup(f => f.CreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_providerMock.Object);

        _metadataEnrichmentServiceMock.Setup(m => m.EnrichMetadata(It.IsAny<CreatePaymentDto>(), It.IsAny<Dictionary<string, string>?>()))
            .Returns<CreatePaymentDto, Dictionary<string, string>?>((dto, metadata) => metadata ?? new Dictionary<string, string>());

        _idempotencyServiceMock.Setup(i => i.CheckExistingPaymentAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        _requestHashServiceMock.Setup(h => h.ComputeRequestHash(It.IsAny<CreatePaymentDto>()))
            .Returns("test-hash-12345");

        idempotentRequestRepositoryMock.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotentRequest?)null);

        var stateServiceMock = new Mock<IPaymentStateService>();
        stateServiceMock.Setup(s => s.Transition(It.IsAny<PaymentStatus>(), It.IsAny<PaymentTrigger>()))
            .Returns<PaymentStatus, PaymentTrigger>((status, trigger) =>
            {
                return trigger switch
                {
                    PaymentTrigger.Process => PaymentStatus.Processing,
                    PaymentTrigger.Complete => PaymentStatus.Succeeded,
                    PaymentTrigger.Fail => PaymentStatus.Failed,
                    _ => status
                };
            });

        var featureManagerMock = new Mock<Microsoft.FeatureManagement.IFeatureManager>();
        featureManagerMock.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var metricsRecorderMock = new Mock<IMetricsRecorder>();

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
            stateServiceMock.Object,
            featureManagerMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentOrchestrator>.Instance,
            metricsRecorderMock.Object,
            _regulatoryRulesEngineMock.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldValidateCompliance_WhenCountryCodeIsProvided()
    {
        // Arrange
        var request = new CreatePaymentDto(
            Guid.NewGuid(),
            100.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345",
            CountryCode: "KW");

        var payment = CreateTestPayment();
        _paymentFactoryMock.Setup(f => f.CreatePayment(It.IsAny<CreatePaymentDto>(), It.IsAny<SplitPayment?>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        _regulatoryRulesEngineMock.Setup(e => e.ValidateTransaction("KW", It.IsAny<PaymentEntity>()))
            .Returns(true);

        var paymentRepositoryMock = Mock.Get(_unitOfWorkMock.Object.Payments);
        paymentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity p, CancellationToken ct) => p);
        paymentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var paymentResult = new PaymentResult(true, "TXN-123", null, null);
        _paymentProcessingServiceMock.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentEntity>(), It.IsAny<IPaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentResult);

        // Act
        await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        _regulatoryRulesEngineMock.Verify(
            e => e.ValidateTransaction("KW", It.IsAny<PaymentEntity>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotValidateCompliance_WhenCountryCodeIsNotProvided()
    {
        // Arrange
        var request = new CreatePaymentDto(
            Guid.NewGuid(),
            100.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345",
            CountryCode: null);

        var payment = CreateTestPayment();
        _paymentFactoryMock.Setup(f => f.CreatePayment(It.IsAny<CreatePaymentDto>(), It.IsAny<SplitPayment?>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        var paymentRepositoryMock = Mock.Get(_unitOfWorkMock.Object.Payments);
        paymentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity p, CancellationToken ct) => p);
        paymentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var paymentResult = new PaymentResult(true, "TXN-123", null, null);
        _paymentProcessingServiceMock.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentEntity>(), It.IsAny<IPaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentResult);

        // Act
        await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        _regulatoryRulesEngineMock.Verify(
            e => e.ValidateTransaction(It.IsAny<string>(), It.IsAny<PaymentEntity>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldThrowComplianceException_WhenValidationFails()
    {
        // Arrange
        var request = new CreatePaymentDto(
            Guid.NewGuid(),
            100.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345",
            CountryCode: "KW");

        var payment = CreateTestPayment();
        _paymentFactoryMock.Setup(f => f.CreatePayment(It.IsAny<CreatePaymentDto>(), It.IsAny<SplitPayment?>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        _regulatoryRulesEngineMock.Setup(e => e.ValidateTransaction("KW", It.IsAny<PaymentEntity>()))
            .Returns(false);

        var complianceRule = new ComplianceRule("KW", "CBK", "Central Bank of Kuwait requirements", true, true, true);
        _regulatoryRulesEngineMock.Setup(e => e.GetRule("KW"))
            .Returns(complianceRule);

        // Act
        var act = async () => await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ComplianceException>()
            .WithMessage("*violates KW regulations*");
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldContinueProcessing_WhenComplianceValidationPasses()
    {
        // Arrange
        var request = new CreatePaymentDto(
            Guid.NewGuid(),
            100.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345",
            CountryCode: "IQ");

        var payment = CreateTestPayment();
        _paymentFactoryMock.Setup(f => f.CreatePayment(It.IsAny<CreatePaymentDto>(), It.IsAny<SplitPayment?>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        _regulatoryRulesEngineMock.Setup(e => e.ValidateTransaction("IQ", It.IsAny<PaymentEntity>()))
            .Returns(true);

        var paymentRepositoryMock = Mock.Get(_unitOfWorkMock.Object.Payments);
        paymentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity p, CancellationToken ct) => p);
        paymentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var paymentResult = new PaymentResult(true, "TXN-123", null, null);
        _paymentProcessingServiceMock.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentEntity>(), It.IsAny<IPaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentResult);

        // Act
        var result = await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _paymentProcessingServiceMock.Verify(
            p => p.ProcessPaymentAsync(It.IsAny<PaymentEntity>(), It.IsAny<IPaymentProvider>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldNotValidateCompliance_WhenCountryCodeIsEmpty()
    {
        // Arrange
        var request = new CreatePaymentDto(
            Guid.NewGuid(),
            100.00m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "PROJECT-001",
            "idempotency-key-12345",
            CountryCode: "");

        var payment = CreateTestPayment();
        _paymentFactoryMock.Setup(f => f.CreatePayment(It.IsAny<CreatePaymentDto>(), It.IsAny<SplitPayment?>(), It.IsAny<Dictionary<string, string>>()))
            .Returns(payment);

        var paymentRepositoryMock = Mock.Get(_unitOfWorkMock.Object.Payments);
        paymentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity p, CancellationToken ct) => p);
        paymentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var paymentResult = new PaymentResult(true, "TXN-123", null, null);
        _paymentProcessingServiceMock.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentEntity>(), It.IsAny<IPaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentResult);

        // Act
        await _orchestrator.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        _regulatoryRulesEngineMock.Verify(
            e => e.ValidateTransaction(It.IsAny<string>(), It.IsAny<PaymentEntity>()),
            Times.Never);
    }

    private static PaymentEntity CreateTestPayment()
    {
        var id = PaymentId.NewId();
        var amount = Amount.FromDecimal(100.00m);
        var currency = Currency.USD;
        var paymentMethod = PaymentMethod.CreditCard;
        var provider = PaymentProvider.ZainCash;
        var merchantId = "merchant-123";
        var orderId = "order-456";

        return new PaymentEntity(id, amount, currency, paymentMethod, provider, merchantId, orderId);
    }
}

