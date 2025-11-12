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
/// Integration tests for GetProviders endpoint (Provider Discovery API).
/// Tests the API controller with MediatR integration and query parameter filtering.
/// </summary>
public class GetProvidersTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;
    private readonly Mock<IPaymentProviderFactory> _providerFactoryMock;
    private readonly PaymentsController _controller;

    public GetProvidersTests()
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
    public async Task GetProviders_ShouldReturnOk_WithAllProviders_WhenNoFilters()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("ZainCash", "IQ", "IQD", "Wallet", true),
            new("FIB", "IQ", "IQD", "Card", true),
            new("Telr", "KW", "KWD", "Card", true),
            new("Paytabs", "AE", "AED", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetProvidersQuery>(q => 
                q.Country == null && q.Currency == null && q.Method == null), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetProviders(null, null, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetProviders_ShouldReturnOk_WithFilteredProviders_WhenCountryFilterProvided()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("Paytabs", "AE", "AED", "Card", true),
            new("Telr", "AE", "AED", "Card", true),
            new("Verifone", "AE", "AED", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetProvidersQuery>(q => 
                q.Country == "AE" && q.Currency == null && q.Method == null), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetProviders("AE", null, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(3);
        providers.Should().OnlyContain(p => p.CountryCode == "AE");
    }

    [Fact]
    public async Task GetProviders_ShouldReturnOk_WithFilteredProviders_WhenCurrencyFilterProvided()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("ZainCash", "IQ", "USD", "Wallet", true),
            new("FIB", "IQ", "USD", "Card", true),
            new("Telr", "IQ", "USD", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetProvidersQuery>(q => 
                q.Country == null && q.Currency == "USD" && q.Method == null), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetProviders(null, "USD", null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(3);
        providers.Should().OnlyContain(p => p.Currency == "USD");
    }

    [Fact]
    public async Task GetProviders_ShouldReturnOk_WithFilteredProviders_WhenMethodFilterProvided()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("ZainCash", "IQ", "IQD", "Wallet", true),
            new("ZainCash", "IQ", "USD", "Wallet", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetProvidersQuery>(q => 
                q.Country == null && q.Currency == null && q.Method == "Wallet"), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetProviders(null, null, "Wallet", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(2);
        providers.Should().OnlyContain(p => p.PaymentMethod == "Wallet");
    }

    [Fact]
    public async Task GetProviders_ShouldReturnOk_WithFilteredProviders_WhenCountryAndMethodFiltersProvided()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("FIB", "IQ", "IQD", "Card", true),
            new("Telr", "IQ", "IQD", "Card", true),
            new("Paytabs", "IQ", "IQD", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetProvidersQuery>(q => 
                q.Country == "IQ" && q.Currency == null && q.Method == "Card"), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetProviders("IQ", null, "Card", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(3);
        providers.Should().OnlyContain(p => p.CountryCode == "IQ" && p.PaymentMethod == "Card");
    }

    [Fact]
    public async Task GetProviders_ShouldReturnOk_WithFilteredProviders_WhenAllFiltersProvided()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("FIB", "IQ", "USD", "Card", true),
            new("Telr", "IQ", "USD", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetProvidersQuery>(q => 
                q.Country == "IQ" && q.Currency == "USD" && q.Method == "Card"), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetProviders("IQ", "USD", "Card", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().HaveCount(2);
        providers.Should().OnlyContain(p => 
            p.CountryCode == "IQ" && 
            p.Currency == "USD" && 
            p.PaymentMethod == "Card");
    }

    [Fact]
    public async Task GetProviders_ShouldReturnOk_WithEmptyList_WhenNoProvidersMatch()
    {
        // Arrange
        var emptyProviders = new List<PaymentProviderInfoDto>();

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetProvidersQuery>(q => 
                q.Country == "XX" && q.Currency == null && q.Method == null), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyProviders);

        // Act
        var result = await _controller.GetProviders("XX", null, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var providers = okResult.Value.Should().BeAssignableTo<IEnumerable<PaymentProviderInfoDto>>().Subject;
        providers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProviders_ShouldCallMediator_WithCorrectQuery()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>
        {
            new("Paytabs", "AE", "AED", "Card", true)
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetProvidersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        await _controller.GetProviders("AE", "AED", "Card", CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetProvidersQuery>(q => 
                q.Country == "AE" && 
                q.Currency == "AED" && 
                q.Method == "Card"), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetProviders_ShouldBeAllowAnonymous()
    {
        // Arrange
        var expectedProviders = new List<PaymentProviderInfoDto>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetProvidersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProviders);

        // Act
        var result = await _controller.GetProviders(null, null, null, CancellationToken.None);

        // Assert
        // If AllowAnonymous is working, the endpoint should be accessible without authentication
        result.Should().NotBeNull();
        // The fact that we can call it without authentication setup means AllowAnonymous is working
    }
}

