using FluentAssertions;
using Microsoft.Extensions.Logging;
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
/// Comprehensive unit tests for InitiateThreeDSecureCommandHandler.
/// Tests cover successful initiation, 3DS not required scenarios, validation, and error handling.
/// </summary>
public class InitiateThreeDSecureCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<IThreeDSecureService> _threeDSecureServiceMock;
    private readonly Mock<ILogger<InitiateThreeDSecureCommandHandler>> _loggerMock;
    private readonly InitiateThreeDSecureCommandHandler _handler;

    public InitiateThreeDSecureCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _threeDSecureServiceMock = new Mock<IThreeDSecureService>();
        _loggerMock = new Mock<ILogger<InitiateThreeDSecureCommandHandler>>();

        _unitOfWorkMock.Setup(u => u.Payments).Returns(_paymentRepositoryMock.Object);

        _handler = new InitiateThreeDSecureCommandHandler(
            _unitOfWorkMock.Object,
            _threeDSecureServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnChallenge_When3DSRequired()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var command = new InitiateThreeDSecureCommand(paymentId, returnUrl);

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");

        var cardToken = new CardToken("token-123", "1234", "Visa");
        payment.SetCardToken(cardToken);

        var challenge = new ThreeDSecureChallenge(
            "https://acs.example.com/authenticate",
            "base64-pareq",
            "merchant-data-123",
            returnUrl,
            "2.2.0");

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                paymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _threeDSecureServiceMock.Setup(s => s.IsAuthenticationRequiredAsync(
                payment.Amount,
                payment.Currency,
                cardToken,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _threeDSecureServiceMock.Setup(s => s.InitiateAuthenticationAsync(
                paymentId,
                payment.Amount,
                payment.Currency,
                cardToken,
                returnUrl,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(challenge);

        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.AcsUrl.Should().Be(challenge.AcsUrl);
        result.Pareq.Should().Be(challenge.Pareq);
        result.Md.Should().Be(challenge.Md);
        result.TermUrl.Should().Be(challenge.TermUrl);
        result.Version.Should().Be(challenge.Version);

        payment.ThreeDSecureStatus.Should().Be(ThreeDSecureStatus.ChallengeRequired);
        _paymentRepositoryMock.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_When3DSNotRequired()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var command = new InitiateThreeDSecureCommand(paymentId, returnUrl);

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(10.00m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");

        var cardToken = new CardToken("token-123", "1234", "Visa");
        payment.SetCardToken(cardToken);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                paymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _threeDSecureServiceMock.Setup(s => s.IsAuthenticationRequiredAsync(
                payment.Amount,
                payment.Currency,
                cardToken,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        payment.ThreeDSecureStatus.Should().Be(ThreeDSecureStatus.Skipped);
        _paymentRepositoryMock.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenChallengeIsNull()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var command = new InitiateThreeDSecureCommand(paymentId, returnUrl);

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");

        var cardToken = new CardToken("token-123", "1234", "Visa");
        payment.SetCardToken(cardToken);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                paymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _threeDSecureServiceMock.Setup(s => s.IsAuthenticationRequiredAsync(
                payment.Amount,
                payment.Currency,
                cardToken,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _threeDSecureServiceMock.Setup(s => s.InitiateAuthenticationAsync(
                paymentId,
                payment.Amount,
                payment.Currency,
                cardToken,
                returnUrl,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ThreeDSecureChallenge?)null);

        _paymentRepositoryMock.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        payment.ThreeDSecureStatus.Should().Be(ThreeDSecureStatus.Skipped);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenPaymentNotFound()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var command = new InitiateThreeDSecureCommand(paymentId, returnUrl);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                paymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Payment {paymentId} not found");
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperationException_WhenCardTokenMissing()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var command = new InitiateThreeDSecureCommand(paymentId, returnUrl);

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");
        // No card token set

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                paymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payment must have a card token to initiate 3D Secure");
    }

    [Fact]
    public async Task Handle_ShouldLogInformation_WhenInitiating3DS()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var returnUrl = "https://example.com/return";
        var command = new InitiateThreeDSecureCommand(paymentId, returnUrl);

        var payment = new PaymentEntity(
            PaymentId.FromGuid(paymentId),
            Amount.FromDecimal(100.50m),
            Currency.USD,
            PaymentMethod.CreditCard,
            PaymentProvider.Stripe,
            "merchant-123",
            "order-456");

        var cardToken = new CardToken("token-123", "1234", "Visa");
        payment.SetCardToken(cardToken);

        _paymentRepositoryMock.Setup(r => r.GetByIdAsync(
                paymentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _threeDSecureServiceMock.Setup(s => s.IsAuthenticationRequiredAsync(
                It.IsAny<Amount>(),
                It.IsAny<Currency>(),
                It.IsAny<CardToken>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initiating 3D Secure")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

