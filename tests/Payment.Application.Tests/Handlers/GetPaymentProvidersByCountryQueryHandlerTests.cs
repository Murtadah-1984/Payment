using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Payment.Application.Common;
using Payment.Application.DTOs;
using Payment.Application.Handlers;
using Payment.Application.Queries;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Application.Tests.Handlers;

/// <summary>
/// Unit tests for GetPaymentProvidersByCountryQueryHandler.
/// Tests country-based payment provider discovery with configuration support.
/// </summary>
public class GetPaymentProvidersByCountryQueryHandlerTests
{
    private readonly Mock<ILogger<GetPaymentProvidersByCountryQueryHandler>> _loggerMock;
    private readonly Mock<IOptions<PaymentProviderCatalogOptions>> _optionsMock;

    public GetPaymentProvidersByCountryQueryHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GetPaymentProvidersByCountryQueryHandler>>();
        _optionsMock = new Mock<IOptions<PaymentProviderCatalogOptions>>();
        
        // Reset catalog to default state before each test
        PaymentProviderCatalog.Reset();
    }

    [Fact]
    public async Task Handle_ShouldReturnProviders_ForIraq()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null!);
        var handler = new GetPaymentProvidersByCountryQueryHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetPaymentProvidersByCountryQuery("IQ");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain(p => p.ProviderName == "ZainCash" && p.CountryCode == "IQ");
        result.Should().Contain(p => p.ProviderName == "FIB" && p.CountryCode == "IQ");
        result.Should().OnlyContain(p => p.CountryCode == "IQ");
        result.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public async Task Handle_ShouldReturnProviders_ForKuwait()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null!);
        var handler = new GetPaymentProvidersByCountryQueryHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetPaymentProvidersByCountryQuery("KW");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain(p => p.ProviderName == "Telr" && p.CountryCode == "KW");
        result.Should().Contain(p => p.ProviderName == "Paytabs" && p.CountryCode == "KW");
        result.Should().Contain(p => p.ProviderName == "Tap" && p.CountryCode == "KW");
        result.Should().OnlyContain(p => p.CountryCode == "KW");
        result.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public async Task Handle_ShouldReturnProviders_ForUAE()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null!);
        var handler = new GetPaymentProvidersByCountryQueryHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetPaymentProvidersByCountryQuery("AE");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain(p => p.ProviderName == "Telr" && p.CountryCode == "AE");
        result.Should().Contain(p => p.ProviderName == "Paytabs" && p.CountryCode == "AE");
        result.Should().Contain(p => p.ProviderName == "Verifone" && p.CountryCode == "AE");
        result.Should().OnlyContain(p => p.CountryCode == "AE");
        result.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_ForUnsupportedCountry()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null!);
        var handler = new GetPaymentProvidersByCountryQueryHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetPaymentProvidersByCountryQuery("XX");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldLoadFromConfiguration_WhenProvided()
    {
        // Arrange
        var configuredOptions = new PaymentProviderCatalogOptions
        {
            Providers = new List<PaymentProviderInfoConfiguration>
            {
                new() { ProviderName = "CustomProvider", CountryCode = "US", Currency = "USD", PaymentMethod = "Card", IsActive = true },
                new() { ProviderName = "CustomProvider2", CountryCode = "US", Currency = "USD", PaymentMethod = "Wallet", IsActive = true }
            }
        };
        _optionsMock.Setup(o => o.Value).Returns(configuredOptions);
        var handler = new GetPaymentProvidersByCountryQueryHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetPaymentProvidersByCountryQuery("US");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.ProviderName == "CustomProvider");
        result.Should().Contain(p => p.ProviderName == "CustomProvider2");
        result.Should().OnlyContain(p => p.CountryCode == "US");
    }

    [Fact]
    public async Task Handle_ShouldFilterInactiveProviders()
    {
        // Arrange
        var configuredOptions = new PaymentProviderCatalogOptions
        {
            Providers = new List<PaymentProviderInfoConfiguration>
            {
                new() { ProviderName = "ActiveProvider", CountryCode = "US", Currency = "USD", PaymentMethod = "Card", IsActive = true },
                new() { ProviderName = "InactiveProvider", CountryCode = "US", Currency = "USD", PaymentMethod = "Card", IsActive = false }
            }
        };
        _optionsMock.Setup(o => o.Value).Returns(configuredOptions);
        var handler = new GetPaymentProvidersByCountryQueryHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetPaymentProvidersByCountryQuery("US");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(p => p.ProviderName == "ActiveProvider");
        result.Should().NotContain(p => p.ProviderName == "InactiveProvider");
    }

    [Fact]
    public async Task Handle_ShouldHandleCaseInsensitiveCountryCode()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null!);
        var handler = new GetPaymentProvidersByCountryQueryHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetPaymentProvidersByCountryQuery("iq"); // lowercase

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.CountryCode == "IQ");
    }
}

