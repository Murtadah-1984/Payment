using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.API.Controllers;
using Payment.Application.DTOs;
using Payment.Application.Queries;
using Payment.Application.Services;
using Xunit;

namespace Payment.API.Tests.Controllers;

/// <summary>
/// Integration tests for GetPaymentProvidersByCountry endpoint.
/// Tests the API controller with MediatR integration.
/// </summary>
public class GetPaymentProvidersByCountryTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly PaymentsController _controller;

    public GetPaymentProvidersByCountryTests()
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
    public async Task GetPaymentProvidersByCountry_ShouldReturnOk_WithProvidersForIraq()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("ZainCash", "IQ", "IQD", "Wallet", true),
            new("FIB", "IQ", "IQD", "Card", true),
            new("Telr", "IQ", "IQD", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetPaymentProvidersByCountryQuery>(q => q.CountryCode == "IQ"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetPaymentProvidersByCountry("IQ", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(3);
        providers.Should().Contain(p => p.ProviderName == "ZainCash");
        providers.Should().Contain(p => p.ProviderName == "FIB");
    }

    [Fact]
    public async Task GetPaymentProvidersByCountry_ShouldReturnOk_WithProvidersForKuwait()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("Telr", "KW", "KWD", "Card", true),
            new("Paytabs", "KW", "KWD", "Card", true),
            new("Tap", "KW", "KWD", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetPaymentProvidersByCountryQuery>(q => q.CountryCode == "KW"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetPaymentProvidersByCountry("KW", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(3);
        providers.Should().Contain(p => p.ProviderName == "Telr");
        providers.Should().Contain(p => p.ProviderName == "Paytabs");
    }

    [Fact]
    public async Task GetPaymentProvidersByCountry_ShouldReturnOk_WithProvidersForUAE()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("Telr", "AE", "AED", "Card", true),
            new("Paytabs", "AE", "AED", "Card", true),
            new("Verifone", "AE", "AED", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetPaymentProvidersByCountryQuery>(q => q.CountryCode == "AE"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetPaymentProvidersByCountry("AE", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(3);
        providers.Should().Contain(p => p.ProviderName == "Verifone");
    }

    [Fact]
    public async Task GetPaymentProvidersByCountry_ShouldReturnOk_WithEmptyList_ForUnsupportedCountry()
    {
        // Arrange
        var emptyProviders = new List<PaymentProviderInfoDto>();

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetPaymentProvidersByCountryQuery>(q => q.CountryCode == "XX"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyProviders);

        // Act
        var result = await _controller.GetPaymentProvidersByCountry("XX", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPaymentProvidersByCountry_ShouldReturnBadRequest_ForInvalidCountryCode()
    {
        // Act
        var result = await _controller.GetPaymentProvidersByCountry("", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetPaymentProvidersByCountryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPaymentProvidersByCountry_ShouldReturnBadRequest_ForTooLongCountryCode()
    {
        // Act
        var result = await _controller.GetPaymentProvidersByCountry("USA", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetPaymentProvidersByCountryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPaymentProvidersByCountry_ShouldConvertToUpperCase()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("ZainCash", "IQ", "IQD", "Wallet", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetPaymentProvidersByCountryQuery>(q => q.CountryCode == "IQ"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetPaymentProvidersByCountry("iq", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mediatorMock.Verify(m => m.Send(It.Is<GetPaymentProvidersByCountryQuery>(q => q.CountryCode == "IQ"), It.IsAny<CancellationToken>()), Times.Once);
    }
}

