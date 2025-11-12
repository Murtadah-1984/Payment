using System.Diagnostics;
using FluentAssertions;
using Payment.Application.Handlers;
using Payment.Application.Queries;
using Xunit;

namespace Payment.API.Tests.OpenTelemetry;

/// <summary>
/// Integration tests for OpenTelemetry tracing (Observability #15).
/// Tests that ActivitySource is properly configured and activities are created.
/// </summary>
public class OpenTelemetryTracingTests
{
    [Fact]
    public void ActivitySource_ShouldBeRegistered_ForPaymentApplication()
    {
        // Arrange & Act
        var sources = ActivitySource.GetSources();

        // Assert
        sources.Should().Contain(s => s.Name == "Payment.Application");
    }

    [Fact]
    public void ActivitySource_ShouldCreateActivity_WhenStarted()
    {
        // Arrange
        var activitySource = new ActivitySource("Payment.Application.Test");

        // Act
        using var activity = activitySource.StartActivity("TestActivity");

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("TestActivity");
    }

    [Fact]
    public void Activity_ShouldSupportTags_WhenCreated()
    {
        // Arrange
        var activitySource = new ActivitySource("Payment.Application.Test");

        // Act
        using var activity = activitySource.StartActivity("TestActivity");
        activity?.SetTag("payment.id", "test-id");
        activity?.SetTag("payment.status", "Pending");

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("payment.id").Should().Be("test-id");
        activity.GetTagItem("payment.status").Should().Be("Pending");
    }

    [Fact]
    public void Activity_ShouldSupportStatus_WhenCreated()
    {
        // Arrange
        var activitySource = new ActivitySource("Payment.Application.Test");

        // Act
        using var activity = activitySource.StartActivity("TestActivity");
        activity?.SetStatus(ActivityStatusCode.Ok);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Ok);
    }
}

