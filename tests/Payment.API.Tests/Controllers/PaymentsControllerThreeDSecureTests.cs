using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Controllers;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Xunit;

namespace Payment.API.Tests.Controllers;

/// <summary>
/// Comprehensive unit tests for 3D Secure endpoints in PaymentsController.
/// Tests cover initiation, callback (POST/GET), error handling, and edge cases.
/// </summary>
public class PaymentsControllerThreeDSecureTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly PaymentsController _controller;

    public PaymentsControllerThreeDSecureTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<PaymentsController>>();
        _providerFactoryMock = new Mock<IPaymentProviderFactory>();

        _controller = new PaymentsController(
            _mediatorMock.Object,
            _loggerMock.Object,
            _providerFactoryMock.Object);
    }

    #region InitiateThreeDSecure Tests

    [Fact]
    public async Task InitiateThreeDSecure_ShouldReturnOk_WithChallenge_When3DSRequired()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var request = new InitiateThreeDSecureDto(returnUrl);

        var expectedChallenge = new ThreeDSecureChallengeDto(
            "https://acs.example.com/authenticate",
            "base64-encoded-pareq",
            "merchant-data-123",
            "https://api.payment.com/api/v1/payments/{id}/3ds/callback",
            "2.2.0");

        _mediatorMock.Setup(m => m.Send(
                It.Is<InitiateThreeDSecureCommand>(c => c.PaymentId == paymentId && c.ReturnUrl == returnUrl),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChallenge);

        // Act
        var result = await _controller.InitiateThreeDSecure(paymentId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedChallenge);
        _mediatorMock.Verify(m => m.Send(
            It.Is<InitiateThreeDSecureCommand>(c => c.PaymentId == paymentId && c.ReturnUrl == returnUrl),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiateThreeDSecure_ShouldReturnOk_WithMessage_When3DSNotRequired()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var request = new InitiateThreeDSecureDto(returnUrl);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<InitiateThreeDSecureCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ThreeDSecureChallengeDto?)null);

        // Act
        var result = await _controller.InitiateThreeDSecure(paymentId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value;
        response.Should().NotBeNull();
        response.Should().BeOfType<object>();
    }

    [Fact]
    public async Task InitiateThreeDSecure_ShouldReturnBadRequest_WhenInvalidOperationException()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var request = new InitiateThreeDSecureDto(returnUrl);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<InitiateThreeDSecureCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Payment not found"));

        // Act
        var result = await _controller.InitiateThreeDSecure(paymentId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task InitiateThreeDSecure_ShouldLogInformation_WhenCalled()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var request = new InitiateThreeDSecureDto(returnUrl);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<InitiateThreeDSecureCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ThreeDSecureChallengeDto?)null);

        // Act
        await _controller.InitiateThreeDSecure(paymentId, request, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initiating 3D Secure")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region CompleteThreeDSecure Tests

    [Fact]
    public async Task CompleteThreeDSecure_ShouldReturnOk_WithResult_WhenPOSTRequest()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var requestBody = new CompleteThreeDSecureDto(
            "base64-pareq",
            "base64-ares",
            "merchant-data-123");

        var expectedResult = new ThreeDSecureResultDto(
            authenticated: true,
            cavv: "cavv-123",
            eci: "05",
            xid: "xid-123",
            version: "2.2.0",
            failureReason: null);

        _mediatorMock.Setup(m => m.Send(
                It.Is<CompleteThreeDSecureCommand>(c => 
                    c.PaymentId == paymentId &&
                    c.Pareq == requestBody.Pareq &&
                    c.Ares == requestBody.Ares &&
                    c.Md == requestBody.Md),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.CompleteThreeDSecure(
            paymentId,
            requestBody,
            null, null, null,
            CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedResult);
    }

    [Fact]
    public async Task CompleteThreeDSecure_ShouldReturnOk_WithResult_WhenGETRequest()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var pareq = "base64-pareq";
        var ares = "base64-ares";
        var md = "merchant-data-123";

        var expectedResult = new ThreeDSecureResultDto(
            authenticated: true,
            cavv: "cavv-123",
            eci: "05",
            xid: "xid-123",
            version: "2.2.0",
            failureReason: null);

        _mediatorMock.Setup(m => m.Send(
                It.Is<CompleteThreeDSecureCommand>(c => 
                    c.PaymentId == paymentId &&
                    c.Pareq == pareq &&
                    c.Ares == ares &&
                    c.Md == md),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.CompleteThreeDSecure(
            paymentId,
            null,
            pareq, ares, md,
            CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedResult);
    }

    [Fact]
    public async Task CompleteThreeDSecure_ShouldReturnBadRequest_WhenMissingParameters()
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        // Act
        var result = await _controller.CompleteThreeDSecure(
            paymentId,
            null,
            null, null, null,
            CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteThreeDSecure_ShouldReturnBadRequest_WhenInvalidOperationException()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var requestBody = new CompleteThreeDSecureDto(
            "base64-pareq",
            "base64-ares",
            "merchant-data-123");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<CompleteThreeDSecureCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Payment not found"));

        // Act
        var result = await _controller.CompleteThreeDSecure(
            paymentId,
            requestBody,
            null, null, null,
            CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteThreeDSecure_ShouldPreferPOSTBody_WhenBothProvided()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var requestBody = new CompleteThreeDSecureDto(
            "body-pareq",
            "body-ares",
            "body-md");

        var expectedResult = new ThreeDSecureResultDto(
            authenticated: true,
            cavv: null,
            eci: null,
            xid: null,
            version: null,
            failureReason: null);

        _mediatorMock.Setup(m => m.Send(
                It.Is<CompleteThreeDSecureCommand>(c => 
                    c.Pareq == "body-pareq" &&
                    c.Ares == "body-ares" &&
                    c.Md == "body-md"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.CompleteThreeDSecure(
            paymentId,
            requestBody,
            "query-pareq", "query-ares", "query-md",
            CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _mediatorMock.Verify(m => m.Send(
            It.Is<CompleteThreeDSecureCommand>(c => 
                c.Pareq == "body-pareq" &&
                c.Ares == "body-ares" &&
                c.Md == "body-md"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteThreeDSecure_ShouldLogInformation_WhenCalled()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var requestBody = new CompleteThreeDSecureDto(
            "base64-pareq",
            "base64-ares",
            "merchant-data-123");

        var expectedResult = new ThreeDSecureResultDto(
            authenticated: true,
            cavv: null,
            eci: null,
            xid: null,
            version: null,
            failureReason: null);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<CompleteThreeDSecureCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        await _controller.CompleteThreeDSecure(
            paymentId,
            requestBody,
            null, null, null,
            CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Completing 3D Secure")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}

