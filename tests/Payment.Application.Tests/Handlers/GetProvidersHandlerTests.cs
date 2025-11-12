using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Handlers;
using Payment.Application.Queries;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Application.Tests.Handlers;

/// <summary>
/// Unit tests for GetProvidersHandler.
/// Tests provider discovery with optional filtering by country, currency, and payment method.
/// </summary>
public class GetProvidersHandlerTests
{
    private readonly Mock<ILogger<GetProvidersHandler>> _loggerMock;
    private readonly Mock<IOptions<PaymentProviderCatalogOptions>> _optionsMock;

    public GetProvidersHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GetProvidersHandler>>();
        _optionsMock = new Mock<IOptions<PaymentProviderCatalogOptions>>();
        
        // Reset catalog to default state before each test
        PaymentProviderCatalog.Reset();
    }

    [Fact]
    public async Task Handle_ShouldReturnAllProviders_WhenNoFiltersProvided()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery(null, null, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.IsActive);
        // Should contain providers from multiple countries
        result.Should().Contain(p => p.CountryCode == "IQ");
        result.Should().Contain(p => p.CountryCode == "KW");
        result.Should().Contain(p => p.CountryCode == "AE");
    }

    [Fact]
    public async Task Handle_ShouldFilterByCountry_WhenCountryProvided()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery("AE", null, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.CountryCode == "AE");
        result.Should().Contain(p => p.ProviderName == "Paytabs" && p.CountryCode == "AE");
        result.Should().Contain(p => p.ProviderName == "Telr" && p.CountryCode == "AE");
        result.Should().Contain(p => p.ProviderName == "Verifone" && p.CountryCode == "AE");
    }

    [Fact]
    public async Task Handle_ShouldFilterByCurrency_WhenCurrencyProvided()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery(null, "USD", null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.Currency == "USD");
        result.Should().Contain(p => p.ProviderName == "ZainCash" && p.Currency == "USD");
        result.Should().Contain(p => p.ProviderName == "FIB" && p.Currency == "USD");
    }

    [Fact]
    public async Task Handle_ShouldFilterByPaymentMethod_WhenMethodProvided()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery(null, null, "Wallet");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.PaymentMethod == "Wallet");
        result.Should().Contain(p => p.ProviderName == "ZainCash" && p.PaymentMethod == "Wallet");
    }

    [Fact]
    public async Task Handle_ShouldFilterByCountryAndMethod_WhenBothProvided()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery("IQ", null, "Card");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.CountryCode == "IQ" && p.PaymentMethod == "Card");
        result.Should().Contain(p => p.ProviderName == "FIB" && p.CountryCode == "IQ" && p.PaymentMethod == "Card");
        result.Should().Contain(p => p.ProviderName == "Telr" && p.CountryCode == "IQ" && p.PaymentMethod == "Card");
        result.Should().NotContain(p => p.PaymentMethod == "Wallet");
    }

    [Fact]
    public async Task Handle_ShouldFilterByAllFilters_WhenAllProvided()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery("IQ", "USD", "Card");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => 
            p.CountryCode == "IQ" && 
            p.Currency == "USD" && 
            p.PaymentMethod == "Card");
        result.Should().Contain(p => p.ProviderName == "FIB");
        result.Should().Contain(p => p.ProviderName == "Telr");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoProvidersMatchFilters()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery("XX", null, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenCurrencyNotFound()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery(null, "EUR", null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenMethodNotFound()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery(null, null, "BankTransfer");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldHandleCaseInsensitiveCountryFilter()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery("ae", null, null); // lowercase

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.CountryCode == "AE");
    }

    [Fact]
    public async Task Handle_ShouldHandleCaseInsensitiveCurrencyFilter()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery(null, "usd", null); // lowercase

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.Currency == "USD");
    }

    [Fact]
    public async Task Handle_ShouldHandleCaseInsensitiveMethodFilter()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery(null, null, "card"); // lowercase

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.PaymentMethod == "Card");
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
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery("US", null, null);

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
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery("US", null, null);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(p => p.ProviderName == "ActiveProvider");
        result.Should().NotContain(p => p.ProviderName == "InactiveProvider");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyStringFilters_AsNoFilter()
    {
        // Arrange
        _optionsMock.Setup(o => o.Value).Returns((PaymentProviderCatalogOptions?)null);
        var handler = new GetProvidersHandler(_loggerMock.Object, _optionsMock.Object);
        var query = new GetProvidersQuery("", "", "");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        // Should return all providers when filters are empty strings
        result.Should().Contain(p => p.CountryCode == "IQ");
        result.Should().Contain(p => p.CountryCode == "KW");
    }
}

