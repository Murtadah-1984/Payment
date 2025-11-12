using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Controllers;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Queries;
using Payment.Application.Services;
using Xunit;

namespace Payment.API.Tests.Controllers;

/// <summary>
/// Controller tests for Tap-to-Pay payment creation.
/// Tests the API endpoint behavior with Tap-to-Pay specific fields.
/// </summary>
public class PaymentsControllerTapToPayTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly PaymentsController _controller;

    public PaymentsControllerTapToPayTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<PaymentsController>>();
        _providerFactoryMock = new Mock<IPaymentProviderFactory>();
        
        _controller = new PaymentsController(
            _mediatorMock.Object,
            _loggerMock.Object,
            _providerFactoryMock.Object);
    }

    [Fact]
    public async Task CreatePayment_ShouldAcceptTapToPayRequest_WithNfcToken()
    {
        // Arrange
        var dto = new CreatePaymentDto(
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
            "device-123",
            "customer-456");

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "MRC-001",
            "ORD-10001",
            "Completed",
            "chg_test_1234567890",
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
        
        // Verify mediator was called with correct command
        _mediatorMock.Verify(m => m.Send(
            It.Is<CreatePaymentCommand>(cmd => 
                cmd.Provider == "TapToPay" &&
                cmd.PaymentMethod == "TapToPay" &&
                cmd.NfcToken == "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token" &&
                cmd.DeviceId == "device-123" &&
                cmd.CustomerId == "customer-456"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePayment_ShouldPassAllTapToPayFields_ToCommand()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var deviceId = "device-xyz-123";
        var customerId = "customer-456";
        
        var dto = new CreatePaymentDto(
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
            deviceId,
            customerId);

        var expectedDto = new PaymentDto(
            Guid.NewGuid(),
            100.00m,
            "IQD",
            "TapToPay",
            "TapToPay",
            "MRC-001",
            "ORD-10001",
            "Completed",
            "chg_test_1234567890",
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(It.IsAny<IRequest<PaymentDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _controller.CreatePayment(dto, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<CreatePaymentCommand>(cmd => 
                cmd.NfcToken == nfcToken &&
                cmd.DeviceId == deviceId &&
                cmd.CustomerId == customerId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

