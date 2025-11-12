using FluentAssertions;
using Moq;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.DependencyInjection;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.Application.Tests.Services;

/// <summary>
/// Comprehensive tests for Feature Flags in PaymentProviderFactory (Feature Flags #17).
/// Tests NewPaymentProvider feature flag behavior.
/// </summary>
public class PaymentProviderFactoryFeatureFlagsTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IFeatureManager> _featureManagerMock;
    private readonly PaymentProviderFactory _factory;

    public PaymentProviderFactoryFeatureFlagsTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _featureManagerMock = new Mock<IFeatureManager>();
        _factory = new PaymentProviderFactory(_serviceProviderMock.Object, _featureManagerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenNewProviderRequested_AndFeatureFlagDisabled()
    {
        // Arrange
        var providerName = "Checkout"; // New provider
        _featureManagerMock.Setup(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var act = async () => await _factory.CreateAsync(providerName, CancellationToken.None);
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage($"Payment provider '{providerName}' is a new provider and requires the 'NewPaymentProvider' feature flag to be enabled.");
        
        _featureManagerMock.Verify(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenTapToPayRequested_AndFeatureFlagDisabled()
    {
        // Arrange
        var providerName = "TapToPay"; // New provider
        _featureManagerMock.Setup(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var act = async () => await _factory.CreateAsync(providerName, CancellationToken.None);
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage($"Payment provider '{providerName}' is a new provider and requires the 'NewPaymentProvider' feature flag to be enabled.");
        
        _featureManagerMock.Verify(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnProvider_WhenNewProviderRequested_AndFeatureFlagEnabled()
    {
        // Arrange
        var providerName = "Checkout";
        var mockProvider = CreateMockProvider(providerName);
        
        _featureManagerMock.Setup(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        SetupServiceProvider(providerName, mockProvider);

        // Act
        var result = await _factory.CreateAsync(providerName, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ProviderName.Should().Be(providerName);
        _featureManagerMock.Verify(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnProvider_WhenOldProviderRequested_RegardlessOfFeatureFlag()
    {
        // Arrange
        var providerName = "ZainCash"; // Old provider
        var mockProvider = CreateMockProvider(providerName);
        
        _featureManagerMock.Setup(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Feature flag disabled, but shouldn't matter for old providers
        
        SetupServiceProvider(providerName, mockProvider);

        // Act
        var result = await _factory.CreateAsync(providerName, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ProviderName.Should().Be(providerName);
        // Should not check feature flag for old providers
        _featureManagerMock.Verify(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Checkout")]
    [InlineData("Verifone")]
    [InlineData("Paytabs")]
    [InlineData("Tap")]
    public async Task CreateAsync_ShouldCheckFeatureFlag_ForNewProviders(string providerName)
    {
        // Arrange
        var mockProvider = CreateMockProvider(providerName);
        _featureManagerMock.Setup(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        SetupServiceProvider(providerName, mockProvider);

        // Act
        var result = await _factory.CreateAsync(providerName, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _featureManagerMock.Verify(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("ZainCash")]
    [InlineData("FIB")]
    [InlineData("Stripe")]
    [InlineData("Telr")]
    public async Task CreateAsync_ShouldNotCheckFeatureFlag_ForOldProviders(string providerName)
    {
        // Arrange
        var mockProvider = CreateMockProvider(providerName);
        SetupServiceProvider(providerName, mockProvider);

        // Act
        var result = await _factory.CreateAsync(providerName, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _featureManagerMock.Verify(f => f.IsEnabledAsync("NewPaymentProvider", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Create_ShouldWork_ForBackwardCompatibility()
    {
        // Arrange
        var providerName = "ZainCash";
        var mockProvider = CreateMockProvider(providerName);
        SetupServiceProvider(providerName, mockProvider);

        // Act
        var result = _factory.Create(providerName);

        // Assert
        result.Should().NotBeNull();
        result.ProviderName.Should().Be(providerName);
    }

    private void SetupServiceProvider(string providerName, IPaymentProvider provider)
    {
        var services = new List<IPaymentProvider> { provider };
        _serviceProviderMock.Setup(sp => sp.GetServices<IPaymentProvider>())
            .Returns(services);
    }

    private static IPaymentProvider CreateMockProvider(string providerName)
    {
        var mock = new Mock<IPaymentProvider>();
        mock.Setup(p => p.ProviderName).Returns(providerName);
        return mock.Object;
    }
}

