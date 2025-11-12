using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Controllers;
using Payment.Application.DTOs;
using Payment.Application.Queries;
using Payment.Application.Services;
using Payment.Domain.Exceptions;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Providers;
using Xunit;

namespace Payment.API.Tests.Controllers;

public class PaymentsControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly PaymentsController _controller;

    public PaymentsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<PaymentsController>>();
        _providerFactoryMock = new Mock<IPaymentProviderFactory>();
        // Create a real service provider with empty collection for provider resolution
        var serviceCollection = new ServiceCollection();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        _controller = new PaymentsController(
            _mediatorMock.Object,
            _loggerMock.Object,
            _providerFactoryMock.Object);
    }

    [Fact]
    public async Task GetProviders_ShouldReturnOk_WithProviders()
    {
        // Arrange
        var providers = new List<PaymentProviderInfoDto>
        {
            new PaymentProviderInfoDto("ZainCash", "KW", "USD", "Wallet", true),
            new PaymentProviderInfoDto("Helcim", "US", "USD", "Card", true),
            new PaymentProviderInfoDto("Stripe", "US", "USD", "Card", true)
        };
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetProvidersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providers);

        // Act
        var result = await _controller.GetProviders(null, null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(providers);
    }

    [Fact]
    public async Task CreatePayment_ShouldReturnCreated_WhenValid()
    {
        // Arrange
        var dto = new CreatePaymentDto(
            Guid.NewGuid(),
            100.50m,
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
            null);

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(It.IsAny<IRequest<PaymentDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.CreatePayment(dto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdAtResult = result.Result as CreatedAtActionResult;
        createdAtResult!.Value.Should().Be(expectedDto);
    }

    [Fact]
    public async Task GetPaymentById_ShouldReturnOk_WhenPaymentExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var expectedDto = new PaymentDto(
            id,
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetPaymentByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Payment.Domain.Common.Result<PaymentDto>.Success(expectedDto));

        // Act
        var result = await _controller.GetPaymentById(id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedDto);
    }

    [Fact]
    public async Task GetPaymentById_ShouldReturnNotFound_WhenPaymentDoesNotExist()
    {
        // Arrange
        var id = Guid.NewGuid();
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetPaymentByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Payment.Domain.Common.Result<PaymentDto>.Failure(Payment.Domain.Common.ErrorCodes.PaymentNotFound, "Payment not found"));

        // Act
        var result = await _controller.GetPaymentById(id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreatePayment_ShouldReturnBadRequest_WhenFraudDetectionBlocksPayment()
    {
        // Arrange
        var dto = new CreatePaymentDto(
            Guid.NewGuid(),
            5000.00m,
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
            null);

        var fraudResult = FraudCheckResult.HighRisk(
            0.95m,
            new[] { "Suspicious IP", "Velocity check failed" },
            "txn-789");

        var fraudException = new FraudDetectionException(
            "Payment blocked due to high fraud risk. Risk score: 0.95. Reasons: Suspicious IP, Velocity check failed",
            fraudResult);

        _mediatorMock.Setup(m => m.Send(It.IsAny<IRequest<PaymentDto>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(fraudException);

        // Act
        var result = await _controller.CreatePayment(dto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPaymentByOrderId_ShouldReturnOk_WhenPaymentExists()
    {
        // Arrange
        var orderId = "order-456";
        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            orderId,
            "Pending",
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetPaymentByOrderIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetPaymentByOrderId(orderId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedDto);
    }

    [Fact]
    public async Task GetPaymentByOrderId_ShouldReturnNotFound_WhenPaymentDoesNotExist()
    {
        // Arrange
        var orderId = "order-456";
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetPaymentByOrderIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentDto?)null);

        // Act
        var result = await _controller.GetPaymentByOrderId(orderId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetPaymentsByMerchant_ShouldReturnOk_WithPayments()
    {
        // Arrange
        var merchantId = "merchant-123";
        var expectedDtos = new[]
        {
            new PaymentDto(
                Guid.NewGuid(),
                100.50m,
                "USD",
                "CreditCard",
                "ZainCash",
                merchantId,
                "order-456",
                "Pending",
                null,
                null,
                null,
                null,
                null,
                DateTime.UtcNow,
                DateTime.UtcNow)
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetPaymentsByMerchantQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDtos);

        // Act
        var result = await _controller.GetPaymentsByMerchant(merchantId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedDtos);
    }

    [Fact]
    public async Task ProcessPayment_ShouldReturnOk_WhenSuccessful()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new ProcessPaymentRequest("txn-123");
        var expectedDto = new PaymentDto(
            id,
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Processing",
            "txn-123",
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(It.IsAny<IRequest<PaymentDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.ProcessPayment(id, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedDto);
    }

    [Fact]
    public async Task CompletePayment_ShouldReturnOk_WhenSuccessful()
    {
        // Arrange
        var id = Guid.NewGuid();
        var expectedDto = new PaymentDto(
            id,
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Completed",
            "txn-123",
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(It.IsAny<IRequest<PaymentDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.CompletePayment(id, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedDto);
    }

    [Fact]
    public async Task FailPayment_ShouldReturnOk_WhenSuccessful()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new FailPaymentRequest("Insufficient funds");
        var expectedDto = new PaymentDto(
            id,
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Failed",
            null,
            "Insufficient funds",
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(It.IsAny<IRequest<PaymentDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.FailPayment(id, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedDto);
    }

    [Fact]
    public async Task RefundPayment_ShouldReturnOk_WhenSuccessful()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new RefundPaymentRequest("refund-txn-123");
        var expectedDto = new PaymentDto(
            id,
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Refunded",
            "refund-txn-123",
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(It.IsAny<IRequest<PaymentDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.RefundPayment(id, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedDto);
    }
}

