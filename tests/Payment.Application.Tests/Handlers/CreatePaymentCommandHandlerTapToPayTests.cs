using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Moq;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Handlers;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.Application.Tests.Handlers;

/// <summary>
/// Integration tests for CreatePaymentCommandHandler with Tap-to-Pay provider.
/// Tests the full flow from command to payment processing.
/// </summary>
public class CreatePaymentCommandHandlerTapToPayTests
{
    private readonly Mock<IPaymentOrchestrator> _orchestratorMock;
    private readonly Mock<IFeatureManager> _featureManagerMock;
    private readonly Mock<ILogger<CreatePaymentCommandHandler>> _loggerMock;
    private readonly CreatePaymentCommandHandler _handler;

    public CreatePaymentCommandHandlerTapToPayTests()
    {
        _orchestratorMock = new Mock<IPaymentOrchestrator>();
        _featureManagerMock = new Mock<IFeatureManager>();
        _loggerMock = new Mock<ILogger<CreatePaymentCommandHandler>>();
        
        _handler = new CreatePaymentCommandHandler(
            _orchestratorMock.Object,
            _featureManagerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldProcessTapToPayPayment_WhenValidCommand()
    {
        // Arrange
        var command = new CreatePaymentCommand(
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

        _orchestratorMock.Setup(o => o.ProcessPaymentAsync(
                It.Is<CreatePaymentDto>(dto => 
                    dto.Provider == "TapToPay" &&
                    dto.PaymentMethod == "TapToPay" &&
                    dto.NfcToken == "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token" &&
                    dto.DeviceId == "device-123" &&
                    dto.CustomerId == "customer-456"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Provider.Should().Be("TapToPay");
        result.PaymentMethod.Should().Be("TapToPay");
        result.Status.Should().Be("Completed");
        result.TransactionId.Should().Be("chg_test_1234567890");
        
        _orchestratorMock.Verify(o => o.ProcessPaymentAsync(
            It.Is<CreatePaymentDto>(dto => 
                dto.NfcToken == command.NfcToken &&
                dto.DeviceId == command.DeviceId &&
                dto.CustomerId == command.CustomerId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPassNfcTokenToOrchestrator()
    {
        // Arrange
        var nfcToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var command = new CreatePaymentCommand(
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
            null,
            null);

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

        _orchestratorMock.Setup(o => o.ProcessPaymentAsync(
                It.IsAny<CreatePaymentDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _orchestratorMock.Verify(o => o.ProcessPaymentAsync(
            It.Is<CreatePaymentDto>(dto => dto.NfcToken == nfcToken),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

