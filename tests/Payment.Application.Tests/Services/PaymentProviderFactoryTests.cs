using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Payment.Application.Services;
using Payment.Domain.Interfaces;
using Xunit;

namespace Payment.Application.Tests.Services;

public class PaymentProviderFactoryTests
{
    [Fact]
    public void Create_ShouldReturnZainCashProvider_WhenZainCashRequested()
    {
        // Arrange
        var services = new ServiceCollection();
        var zainCashProvider = new Mock<IPaymentProvider>();
        zainCashProvider.Setup(p => p.ProviderName).Returns("ZainCash");
        
        services.AddScoped<IPaymentProvider>(_ => zainCashProvider.Object);
        var serviceProvider = services.BuildServiceProvider();
        var factory = new PaymentProviderFactory(serviceProvider);

        // Act
        var result = factory.Create("ZainCash");

        // Assert
        result.Should().NotBeNull();
        result.ProviderName.Should().Be("ZainCash");
    }

    [Fact]
    public void Create_ShouldReturnStripeProvider_WhenStripeRequested()
    {
        // Arrange
        var services = new ServiceCollection();
        var stripeProvider = new Mock<IPaymentProvider>();
        stripeProvider.Setup(p => p.ProviderName).Returns("Stripe");
        
        services.AddScoped<IPaymentProvider>(_ => stripeProvider.Object);
        var serviceProvider = services.BuildServiceProvider();
        var factory = new PaymentProviderFactory(serviceProvider);

        // Act
        var result = factory.Create("Stripe");

        // Assert
        result.Should().NotBeNull();
        result.ProviderName.Should().Be("Stripe");
    }

    [Fact]
    public void Create_ShouldThrowException_WhenProviderNotFound()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new PaymentProviderFactory(serviceProvider);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => factory.Create("NonExistentProvider"));
    }

    [Fact]
    public void GetAvailableProviders_ShouldReturnAllRegisteredProviders()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider1 = new Mock<IPaymentProvider>();
        provider1.Setup(p => p.ProviderName).Returns("ZainCash");
        var provider2 = new Mock<IPaymentProvider>();
        provider2.Setup(p => p.ProviderName).Returns("Stripe");
        
        services.AddScoped<IPaymentProvider>(_ => provider1.Object);
        services.AddScoped<IPaymentProvider>(_ => provider2.Object);
        var serviceProvider = services.BuildServiceProvider();
        var factory = new PaymentProviderFactory(serviceProvider);

        // Act
        var result = factory.GetAvailableProviders().ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("ZainCash");
        result.Should().Contain("Stripe");
    }
}

