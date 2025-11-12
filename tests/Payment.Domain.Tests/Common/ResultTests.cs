using FluentAssertions;
using Payment.Domain.Common;
using Xunit;

namespace Payment.Domain.Tests.Common;

/// <summary>
/// Unit tests for Result pattern (Result Pattern #16).
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult_WithValue()
    {
        // Arrange & Act
        var result = Result<string>.Success("test-value");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test-value");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult_WithError()
    {
        // Arrange & Act
        var error = new Error("TEST_ERROR", "Test error message");
        var result = Result<string>.Failure(error);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TEST_ERROR");
        result.Error.Message.Should().Be("Test error message");
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult_WithCodeAndMessage()
    {
        // Arrange & Act
        var result = Result<string>.Failure("TEST_ERROR", "Test error message");

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TEST_ERROR");
        result.Error.Message.Should().Be("Test error message");
    }

    [Fact]
    public void Match_ShouldCallOnSuccess_WhenResultIsSuccess()
    {
        // Arrange
        var result = Result<string>.Success("test-value");

        // Act
        var matched = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Failure: {error.Code}");

        // Assert
        matched.Should().Be("Success: test-value");
    }

    [Fact]
    public void Match_ShouldCallOnFailure_WhenResultIsFailure()
    {
        // Arrange
        var result = Result<string>.Failure("TEST_ERROR", "Test error");

        // Act
        var matched = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Failure: {error.Code}");

        // Assert
        matched.Should().Be("Failure: TEST_ERROR");
    }

    [Fact]
    public async Task MatchAsync_ShouldCallOnSuccess_WhenResultIsSuccess()
    {
        // Arrange
        var result = Result<string>.Success("test-value");

        // Act
        var matched = await result.MatchAsync(
            onSuccess: async value => await Task.FromResult($"Success: {value}"),
            onFailure: async error => await Task.FromResult($"Failure: {error.Code}"));

        // Assert
        matched.Should().Be("Success: test-value");
    }

    [Fact]
    public async Task MatchAsync_ShouldCallOnFailure_WhenResultIsFailure()
    {
        // Arrange
        var result = Result<string>.Failure("TEST_ERROR", "Test error");

        // Act
        var matched = await result.MatchAsync(
            onSuccess: async value => await Task.FromResult($"Success: {value}"),
            onFailure: async error => await Task.FromResult($"Failure: {error.Code}"));

        // Assert
        matched.Should().Be("Failure: TEST_ERROR");
    }

    [Fact]
    public void Result_NonGeneric_ShouldCreateSuccessResult()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Result_NonGeneric_ShouldCreateFailureResult()
    {
        // Arrange & Act
        var error = new Error("TEST_ERROR", "Test error");
        var result = Result.Failure(error);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TEST_ERROR");
    }

    [Fact]
    public void Result_NonGeneric_Match_ShouldCallOnSuccess_WhenResultIsSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var matched = result.Match(
            onSuccess: () => "Success",
            onFailure: error => $"Failure: {error.Code}");

        // Assert
        matched.Should().Be("Success");
    }

    [Fact]
    public void Result_NonGeneric_Match_ShouldCallOnFailure_WhenResultIsFailure()
    {
        // Arrange
        var result = Result.Failure("TEST_ERROR", "Test error");

        // Act
        var matched = result.Match(
            onSuccess: () => "Success",
            onFailure: error => $"Failure: {error.Code}");

        // Assert
        matched.Should().Be("Failure: TEST_ERROR");
    }
}

