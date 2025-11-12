using FluentAssertions;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Domain.Tests.ValueObjects;

/// <summary>
/// Unit tests for PaymentProviderCatalog.
/// Tests static catalog functionality and country-based provider discovery.
/// </summary>
public class PaymentProviderCatalogTests
{
    public PaymentProviderCatalogTests()
    {
        // Reset catalog to default state before each test
        PaymentProviderCatalog.Reset();
    }

    [Fact]
    public void GetProvidersByCountry_ShouldReturnProviders_ForIraq()
    {
        // Act
        var providers = PaymentProviderCatalog.GetProvidersByCountry("IQ");

        // Assert
        providers.Should().NotBeNull();
        providers.Should().NotBeEmpty();
        providers.Should().Contain(p => p.ProviderName == "ZainCash" && p.CountryCode == "IQ");
        providers.Should().Contain(p => p.ProviderName == "FIB" && p.CountryCode == "IQ");
        providers.Should().OnlyContain(p => p.CountryCode == "IQ");
        providers.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public void GetProvidersByCountry_ShouldReturnProviders_ForKuwait()
    {
        // Act
        var providers = PaymentProviderCatalog.GetProvidersByCountry("KW");

        // Assert
        providers.Should().NotBeNull();
        providers.Should().NotBeEmpty();
        providers.Should().Contain(p => p.ProviderName == "Telr" && p.CountryCode == "KW");
        providers.Should().Contain(p => p.ProviderName == "Paytabs" && p.CountryCode == "KW");
        providers.Should().OnlyContain(p => p.CountryCode == "KW");
    }

    [Fact]
    public void GetProvidersByCountry_ShouldReturnProviders_ForUAE()
    {
        // Act
        var providers = PaymentProviderCatalog.GetProvidersByCountry("AE");

        // Assert
        providers.Should().NotBeNull();
        providers.Should().NotBeEmpty();
        providers.Should().Contain(p => p.ProviderName == "Telr" && p.CountryCode == "AE");
        providers.Should().Contain(p => p.ProviderName == "Verifone" && p.CountryCode == "AE");
        providers.Should().OnlyContain(p => p.CountryCode == "AE");
    }

    [Fact]
    public void GetProvidersByCountry_ShouldReturnEmptyList_ForUnsupportedCountry()
    {
        // Act
        var providers = PaymentProviderCatalog.GetProvidersByCountry("XX");

        // Assert
        providers.Should().NotBeNull();
        providers.Should().BeEmpty();
    }

    [Fact]
    public void GetProvidersByCountry_ShouldReturnEmptyList_ForNullCountryCode()
    {
        // Act
        var providers = PaymentProviderCatalog.GetProvidersByCountry(null!);

        // Assert
        providers.Should().NotBeNull();
        providers.Should().BeEmpty();
    }

    [Fact]
    public void GetProvidersByCountry_ShouldReturnEmptyList_ForEmptyCountryCode()
    {
        // Act
        var providers = PaymentProviderCatalog.GetProvidersByCountry(string.Empty);

        // Assert
        providers.Should().NotBeNull();
        providers.Should().BeEmpty();
    }

    [Fact]
    public void GetAllProvidersByCountry_ShouldIncludeInactiveProviders()
    {
        // Arrange
        var customProviders = new List<PaymentProviderInfo>
        {
            new("Provider1", "US", "USD", "Card", true),
            new("Provider2", "US", "USD", "Card", false)
        };
        PaymentProviderCatalog.Initialize(customProviders);

        // Act
        var providers = PaymentProviderCatalog.GetAllProvidersByCountry("US");

        // Assert
        providers.Should().NotBeNull();
        providers.Should().HaveCount(2);
        providers.Should().Contain(p => p.ProviderName == "Provider1" && p.IsActive);
        providers.Should().Contain(p => p.ProviderName == "Provider2" && !p.IsActive);
    }

    [Fact]
    public void IsCountrySupported_ShouldReturnTrue_ForSupportedCountry()
    {
        // Act
        var isSupported = PaymentProviderCatalog.IsCountrySupported("IQ");

        // Assert
        isSupported.Should().BeTrue();
    }

    [Fact]
    public void IsCountrySupported_ShouldReturnFalse_ForUnsupportedCountry()
    {
        // Act
        var isSupported = PaymentProviderCatalog.IsCountrySupported("XX");

        // Assert
        isSupported.Should().BeFalse();
    }

    [Fact]
    public void IsCountrySupported_ShouldReturnFalse_ForNullCountryCode()
    {
        // Act
        var isSupported = PaymentProviderCatalog.IsCountrySupported(null!);

        // Assert
        isSupported.Should().BeFalse();
    }

    [Fact]
    public void GetSupportedCountries_ShouldReturnListOfCountries()
    {
        // Act
        var countries = PaymentProviderCatalog.GetSupportedCountries();

        // Assert
        countries.Should().NotBeNull();
        countries.Should().NotBeEmpty();
        countries.Should().Contain("IQ");
        countries.Should().Contain("KW");
        countries.Should().Contain("AE");
    }

    [Fact]
    public void Initialize_ShouldReplaceDefaultCatalog()
    {
        // Arrange
        var customProviders = new List<PaymentProviderInfo>
        {
            new("CustomProvider", "US", "USD", "Card", true)
        };

        // Act
        PaymentProviderCatalog.Initialize(customProviders);
        var providers = PaymentProviderCatalog.GetProvidersByCountry("US");

        // Assert
        providers.Should().NotBeNull();
        providers.Should().HaveCount(1);
        providers.Should().Contain(p => p.ProviderName == "CustomProvider");
    }

    [Fact]
    public void Initialize_ShouldFilterInvalidProviders()
    {
        // Arrange
        var customProviders = new List<PaymentProviderInfo>
        {
            new("ValidProvider", "US", "USD", "Card", true),
            new("", "US", "USD", "Card", true), // Invalid: empty provider name
            new("ValidProvider2", "", "USD", "Card", true), // Invalid: empty country code
            new("ValidProvider3", "US", "", "Card", true) // Invalid: empty currency
        };

        // Act
        PaymentProviderCatalog.Initialize(customProviders);
        var providers = PaymentProviderCatalog.GetProvidersByCountry("US");

        // Assert
        providers.Should().NotBeNull();
        providers.Should().HaveCount(1);
        providers.Should().Contain(p => p.ProviderName == "ValidProvider");
    }

    [Fact]
    public void Initialize_ShouldHandleCaseInsensitiveCountryCodes()
    {
        // Arrange
        var customProviders = new List<PaymentProviderInfo>
        {
            new("Provider1", "us", "USD", "Card", true), // lowercase
            new("Provider2", "US", "USD", "Card", true) // uppercase
        };

        // Act
        PaymentProviderCatalog.Initialize(customProviders);
        var providers = PaymentProviderCatalog.GetProvidersByCountry("US");

        // Assert
        providers.Should().NotBeNull();
        providers.Should().HaveCount(2);
    }

    [Fact]
    public void Reset_ShouldRestoreDefaultCatalog()
    {
        // Arrange
        var customProviders = new List<PaymentProviderInfo>
        {
            new("CustomProvider", "US", "USD", "Card", true)
        };
        PaymentProviderCatalog.Initialize(customProviders);

        // Act
        PaymentProviderCatalog.Reset();
        var iqProviders = PaymentProviderCatalog.GetProvidersByCountry("IQ");
        var usProviders = PaymentProviderCatalog.GetProvidersByCountry("US");

        // Assert
        iqProviders.Should().NotBeEmpty();
        usProviders.Should().BeEmpty(); // Custom provider should be gone
    }
}

