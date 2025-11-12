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

    [Fact]
    public void GetAll_ShouldReturnAllActiveProviders_FromAllCountries()
    {
        // Act
        var allProviders = PaymentProviderCatalog.GetAll();

        // Assert
        allProviders.Should().NotBeNull();
        allProviders.Should().NotBeEmpty();
        allProviders.Should().OnlyContain(p => p.IsActive);
        // Should contain providers from multiple countries
        allProviders.Should().Contain(p => p.CountryCode == "IQ");
        allProviders.Should().Contain(p => p.CountryCode == "KW");
        allProviders.Should().Contain(p => p.CountryCode == "AE");
        allProviders.Should().Contain(p => p.CountryCode == "SA");
    }

    [Fact]
    public void GetAll_ShouldReturnFlattenedList_FromAllCountries()
    {
        // Act
        var allProviders = PaymentProviderCatalog.GetAll();

        // Assert
        allProviders.Should().NotBeNull();
        allProviders.Should().NotBeEmpty();
        
        // Verify we have providers from multiple countries in a single list
        var iqCount = allProviders.Count(p => p.CountryCode == "IQ");
        var kwCount = allProviders.Count(p => p.CountryCode == "KW");
        var aeCount = allProviders.Count(p => p.CountryCode == "AE");
        
        iqCount.Should().BeGreaterThan(0);
        kwCount.Should().BeGreaterThan(0);
        aeCount.Should().BeGreaterThan(0);
        
        // Total count should be sum of all countries
        var totalExpected = iqCount + kwCount + aeCount;
        allProviders.Count.Should().BeGreaterThanOrEqualTo(totalExpected);
    }

    [Fact]
    public void GetAll_ShouldExcludeInactiveProviders()
    {
        // Arrange
        var customProviders = new List<PaymentProviderInfo>
        {
            new("ActiveProvider", "US", "USD", "Card", true),
            new("InactiveProvider", "US", "USD", "Card", false)
        };
        PaymentProviderCatalog.Initialize(customProviders);

        // Act
        var allProviders = PaymentProviderCatalog.GetAll();

        // Assert
        allProviders.Should().NotBeNull();
        allProviders.Should().Contain(p => p.ProviderName == "ActiveProvider");
        allProviders.Should().NotContain(p => p.ProviderName == "InactiveProvider");
        allProviders.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public void GetAll_ShouldReturnProviders_AfterInitialization()
    {
        // Arrange
        var customProviders = new List<PaymentProviderInfo>
        {
            new("CustomProvider1", "US", "USD", "Card", true),
            new("CustomProvider2", "CA", "CAD", "Card", true)
        };
        PaymentProviderCatalog.Initialize(customProviders);

        // Act
        var allProviders = PaymentProviderCatalog.GetAll();

        // Assert
        allProviders.Should().NotBeNull();
        allProviders.Should().Contain(p => p.ProviderName == "CustomProvider1" && p.CountryCode == "US");
        allProviders.Should().Contain(p => p.ProviderName == "CustomProvider2" && p.CountryCode == "CA");
    }

    [Fact]
    public void GetAll_ShouldReturnConsistentResults_OnMultipleCalls()
    {
        // Act
        var firstCall = PaymentProviderCatalog.GetAll();
        var secondCall = PaymentProviderCatalog.GetAll();

        // Assert
        firstCall.Should().NotBeNull();
        secondCall.Should().NotBeNull();
        firstCall.Count.Should().Be(secondCall.Count);
        firstCall.Should().BeEquivalentTo(secondCall);
    }
}

