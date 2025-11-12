using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Payment.Application.DTOs;
using Payment.Application.Services;
using Payment.Domain.Enums;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Xunit;

namespace Payment.Application.Tests.Services;

/// <summary>
/// Integration tests for IncidentResponseService.
/// Tests end-to-end scenarios with real service dependencies.
/// </summary>
public class IncidentResponseServiceIntegrationTests
{
    [Fact]
    public async Task AssessPaymentFailureAsync_EndToEnd_ShouldWorkWithAllDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        var paymentRepositoryMock = new Mock<IPaymentRepository>();
        var circuitBreakerServiceMock = new Mock<ICircuitBreakerService>();
        var refundServiceMock = new Mock<IRefundService>();
        var notificationServiceMock = new Mock<INotificationService>();

        services.AddSingleton(paymentRepositoryMock.Object);
        services.AddSingleton(circuitBreakerServiceMock.Object);
        services.AddSingleton(refundServiceMock.Object);
        services.AddSingleton(notificationServiceMock.Object);
        services.AddScoped<IIncidentResponseService, IncidentResponseService>();

        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<IIncidentResponseService>();

        var context = new PaymentFailureContext(
            StartTime: DateTime.UtcNow.AddMinutes(-10),
            EndTime: null,
            Provider: "Stripe",
            FailureType: PaymentFailureType.ProviderUnavailable,
            AffectedPaymentCount: 150,
            Metadata: new Dictionary<string, object>());

        circuitBreakerServiceMock
            .Setup(s => s.GetProvidersWithOpenCircuitBreakersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Stripe" });

        // Act
        var result = await service.AssessPaymentFailureAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().Be(IncidentSeverity.Critical);
        result.AffectedProviders.Should().Contain("Stripe");
        result.RecommendedActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessAutomaticRefundsAsync_EndToEnd_ShouldProcessRefundsThroughRefundService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        var paymentRepositoryMock = new Mock<IPaymentRepository>();
        var circuitBreakerServiceMock = new Mock<ICircuitBreakerService>();
        var refundServiceMock = new Mock<IRefundService>();
        var notificationServiceMock = new Mock<INotificationService>();

        services.AddSingleton(paymentRepositoryMock.Object);
        services.AddSingleton(circuitBreakerServiceMock.Object);
        services.AddSingleton(refundServiceMock.Object);
        services.AddSingleton(notificationServiceMock.Object);
        services.AddScoped<IIncidentResponseService, IncidentResponseService>();

        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<IIncidentResponseService>();

        var paymentIds = new[]
        {
            PaymentId.NewId(),
            PaymentId.NewId()
        };

        var refundResults = new Dictionary<PaymentId, bool>
        {
            { paymentIds[0], true },
            { paymentIds[1], true }
        };

        refundServiceMock
            .Setup(s => s.ProcessRefundsAsync(
                It.IsAny<IEnumerable<PaymentId>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundResults);

        // Act
        var result = await service.ProcessAutomaticRefundsAsync(paymentIds);

        // Assert
        result.Should().HaveCount(2);
        result.Values.All(v => v == true).Should().BeTrue();
        refundServiceMock.Verify(
            s => s.ProcessRefundsAsync(
                It.Is<IEnumerable<PaymentId>>(ids => ids.Count() == 2),
                It.Is<string>(r => r.Contains("Automatic refund")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyStakeholdersAsync_EndToEnd_ShouldSendNotificationThroughNotificationService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        var paymentRepositoryMock = new Mock<IPaymentRepository>();
        var circuitBreakerServiceMock = new Mock<ICircuitBreakerService>();
        var refundServiceMock = new Mock<IRefundService>();
        var notificationServiceMock = new Mock<INotificationService>();

        services.AddSingleton(paymentRepositoryMock.Object);
        services.AddSingleton(circuitBreakerServiceMock.Object);
        services.AddSingleton(refundServiceMock.Object);
        services.AddSingleton(notificationServiceMock.Object);
        services.AddScoped<IIncidentResponseService, IncidentResponseService>();

        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<IIncidentResponseService>();

        notificationServiceMock
            .Setup(s => s.NotifyStakeholdersAsync(
                It.IsAny<IncidentSeverity>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await service.NotifyStakeholdersAsync(
            IncidentSeverity.Critical,
            "Critical payment failure incident detected");

        // Assert
        result.Should().BeTrue();
        notificationServiceMock.Verify(
            s => s.NotifyStakeholdersAsync(
                IncidentSeverity.Critical,
                "Critical payment failure incident detected",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

