using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Payment.API.Extensions;
using Payment.Domain.Common;
using Payment.Application.DTOs;
using Xunit;

namespace Payment.API.Tests.Extensions;

/// <summary>
/// Unit tests for ErrorMappingExtensions (Result Pattern #16).
/// </summary>
public class ErrorMappingExtensionsTests
{
    [Fact]
    public void ToActionResult_ShouldReturnOkObjectResult_WhenResultIsSuccess()
    {
        // Arrange
        var dto = new PaymentDto(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "CreditCard",
            "ZainCash",
            "merchant-123",
            "order-456",
            "Pending",
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);
        var result = Result<PaymentDto>.Success(dto);

        // Act
        var actionResult = result.ToActionResult();

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        var okResult = actionResult as OkObjectResult;
        okResult!.Value.Should().Be(dto);
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void ToActionResult_ShouldReturnNotFound_WhenPaymentNotFound()
    {
        // Arrange
        var result = Result<PaymentDto>.Failure(ErrorCodes.PaymentNotFound, "Payment not found");

        // Act
        var actionResult = result.ToActionResult();

        // Assert
        actionResult.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = actionResult as NotFoundObjectResult;
        notFoundResult!.StatusCode.Should().Be(404);
    }

    [Fact]
    public void ToActionResult_ShouldReturnBadRequest_WhenValidationError()
    {
        // Arrange
        var result = Result<PaymentDto>.Failure(ErrorCodes.InvalidAmount, "Invalid amount");

        // Act
        var actionResult = result.ToActionResult();

        // Assert
        actionResult.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = actionResult as BadRequestObjectResult;
        badRequestResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public void ToActionResult_ShouldReturnConflict_WhenIdempotencyKeyMismatch()
    {
        // Arrange
        var result = Result<PaymentDto>.Failure(ErrorCodes.IdempotencyKeyMismatch, "Idempotency key mismatch");

        // Act
        var actionResult = result.ToActionResult();

        // Assert
        actionResult.Should().BeOfType<ConflictObjectResult>();
        var conflictResult = actionResult as ConflictObjectResult;
        conflictResult!.StatusCode.Should().Be(409);
    }

    [Fact]
    public void ToActionResult_ShouldReturnBadGateway_WhenProviderError()
    {
        // Arrange
        var result = Result<PaymentDto>.Failure(ErrorCodes.ProviderError, "Provider error");

        // Act
        var actionResult = result.ToActionResult();

        // Assert
        actionResult.Should().BeOfType<ObjectResult>();
        var objectResult = actionResult as ObjectResult;
        objectResult!.StatusCode.Should().Be(502);
    }

    [Fact]
    public void ToActionResult_ShouldReturnInternalServerError_WhenUnknownError()
    {
        // Arrange
        var result = Result<PaymentDto>.Failure("UNKNOWN_ERROR", "Unknown error");

        // Act
        var actionResult = result.ToActionResult();

        // Assert
        actionResult.Should().BeOfType<ObjectResult>();
        var objectResult = actionResult as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public void ToActionResult_NonGeneric_ShouldReturnOkResult_WhenResultIsSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var actionResult = result.ToActionResult();

        // Assert
        actionResult.Should().BeOfType<OkResult>();
        var okResult = actionResult as OkResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public void ToActionResult_NonGeneric_ShouldReturnNotFound_WhenPaymentNotFound()
    {
        // Arrange
        var result = Result.Failure(ErrorCodes.PaymentNotFound, "Payment not found");

        // Act
        var actionResult = result.ToActionResult();

        // Assert
        actionResult.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = actionResult as NotFoundObjectResult;
        notFoundResult!.StatusCode.Should().Be(404);
    }
}

