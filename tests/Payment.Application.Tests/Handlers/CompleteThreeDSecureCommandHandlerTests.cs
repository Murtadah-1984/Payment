using FluentAssertions;
using Moq;
using Payment.Application.Commands;
using Payment.Application.Handlers;
using Payment.Domain.Entities;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Application.Tests.Handlers;

/// <summary>
/// Comprehensive unit tests for CompleteThreeDSecureCommandHandler.
/// Tests cover successful completion, authentication failure, validation, and error handling.
/// </summary>
public class CompleteThreeDSecureCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<IThreeDSecureService> _threeDSecureServiceMock;
    private readonly Mock<ILogger<CompleteThreeDSecureCommandHandler>> _loggerMock;
    private readonly CompleteThreeDSecureCommandHandler _handler;

    public CompleteThreeDSecureCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _threeDSecureServiceMock = new Mock<IThreeDSecureService>();
        _loggerMock = new Mock<ILogger<CompleteThreeDSecureCommandHandler>>();

        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentRepositoryMock.Object);

        _handler = new CompleteThreeDSecureCommandHandler(
            _unitOfWorkMock.Object,
            _threeDSecureServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnResult_WhenAuthenticationSuccessful()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var pareq = "base64-pareq";
        var ares = "base64-ares";
        var md = "merchant-data-123";
        var command = new CompleteThreeDSecureCommand(paymentId, pareq, ares, md);

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");

        var challenge = new ThreeDSecureChallenge(
            "https://acs.example.com/authenticate",
            pareq,
            md,
            "https://example.com/return",
            "2.2.0");
        payment.InitiateThreeDSecure(challenge);

        var threeDSResult = new ThreeDSecureResult(
            authenticated: true,
            cavv: "cavv-1234567890123456789012345678",
            eci: "05",
            xid: "xid-12345678901234567890",
            version: "2.2.0");

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                PaymentId.FromGuid(paymentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _threeDSecureServiceMock.Setup(s => s.CompleteAuthenticationAsync(
                paymentId,
                pareq,
                ares,
                md,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(threeDSResult);

        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Authenticated.Should().BeTrue();
        result.Cavv.Should().Be(threeDSResult.Cavv);
        result.Eci.Should().Be(threeDSResult.Eci);
        result.Xid.Should().Be(threeDSResult.Xid);
        result.Version.Should().Be(threeDSResult.Version);
        result.FailureReason.Should().BeNull();

        payment.ThreeDSecureStatus.Should().Be(ThreeDSecureStatus.Authenticated);
        payment.ThreeDSecureCavv.Should().Be(threeDSResult.Cavv);
        payment.ThreeDSecureEci.Should().Be(threeDSResult.Eci);
        payment.ThreeDSecureXid.Should().Be(threeDSResult.Xid);

        _paymentRepositoryMock.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnResult_WhenAuthenticationFailed()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var pareq = "base64-pareq";
        var ares = "base64-ares";
        var md = "merchant-data-123";
        var command = new CompleteThreeDSecureCommand(paymentId, pareq, ares, md);

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");

        var challenge = new ThreeDSecureChallenge(
            "https://acs.example.com/authenticate",
            pareq,
            md,
            "https://example.com/return",
            "2.2.0");
        payment.InitiateThreeDSecure(challenge);

        var threeDSResult = new ThreeDSecureResult(
            authenticated: false,
            failureReason: "Authentication failed");

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                PaymentId.FromGuid(paymentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _threeDSecureServiceMock.Setup(s => s.CompleteAuthenticationAsync(
                paymentId,
                pareq,
                ares,
                md,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(threeDSResult);

        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Authenticated.Should().BeFalse();
        result.FailureReason.Should().Be("Authentication failed");

        payment.ThreeDSecureStatus.Should().Be(ThreeDSecureStatus.Failed);
        payment.FailureReason.Should().Be("Authentication failed");
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenPaymentNotFound()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var command = new CompleteThreeDSecureCommand(
            paymentId,
            "pareq",
            "ares",
            "md");

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                PaymentId.FromGuid(paymentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Payment {paymentId} not found");
    }

    [Fact]
    public async Task Handle_ShouldLogInformation_WhenCompleting3DS()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var pareq = "base64-pareq";
        var ares = "base64-ares";
        var md = "merchant-data-123";
        var command = new CompleteThreeDSecureCommand(paymentId, pareq, ares, md);

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");

        var threeDSResult = new ThreeDSecureResult(
            authenticated: true,
            cavv: "cavv-123",
            eci: "05",
            version: "2.2.0");

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                PaymentId.FromGuid(paymentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _threeDSecureServiceMock.Setup(s => s.CompleteAuthenticationAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(threeDSResult);

        _paymentRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<PaymentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

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
}

