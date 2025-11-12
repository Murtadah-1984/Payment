using Microsoft.AspNetCore.Mvc;
using Payment.Domain.Common;

namespace Payment.API.Extensions;

/// <summary>
/// Extension methods for mapping domain errors to HTTP status codes.
/// Located in Presentation layer as it handles HTTP concerns.
/// </summary>
public static class ErrorMappingExtensions
{
    /// <summary>
    /// Maps a domain error to an appropriate HTTP status code and ActionResult.
    /// </summary>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        return result.Error!.Code switch
        {
            // 404 Not Found
            ErrorCodes.PaymentNotFound or 
            ErrorCodes.PaymentByOrderIdNotFound => new NotFoundObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            }),

            // 400 Bad Request - Validation errors
            ErrorCodes.InvalidAmount or
            ErrorCodes.InvalidCurrency or
            ErrorCodes.InvalidPaymentMethod or
            ErrorCodes.InvalidProvider or
            ErrorCodes.InvalidMerchantId or
            ErrorCodes.InvalidOrderId or
            ErrorCodes.InvalidProjectCode or
            ErrorCodes.InvalidIdempotencyKey or
            ErrorCodes.PaymentAlreadyCompleted or
            ErrorCodes.PaymentAlreadyFailed or
            ErrorCodes.PaymentAlreadyRefunded or
            ErrorCodes.PaymentNotCompleted or
            ErrorCodes.CannotRefundNonCompletedPayment or
            ErrorCodes.CannotFailCompletedPayment or
            ErrorCodes.InvalidPaymentStatus => new BadRequestObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            }),

            // 409 Conflict - Idempotency errors
            ErrorCodes.IdempotencyKeyMismatch => new ConflictObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            }),

            // 502 Bad Gateway - Provider errors
            ErrorCodes.ProviderError or
            ErrorCodes.ProviderTimeout or
            ErrorCodes.ProviderUnavailable => new ObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            })
            {
                StatusCode = 502
            },

            // 500 Internal Server Error - Default for unknown errors
            _ => new ObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            })
            {
                StatusCode = 500
            }
        };
    }

    /// <summary>
    /// Maps a domain error to an appropriate HTTP status code and ActionResult (non-generic).
    /// </summary>
    public static ActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new OkResult();
        }

        return result.Error!.Code switch
        {
            // 404 Not Found
            ErrorCodes.PaymentNotFound or 
            ErrorCodes.PaymentByOrderIdNotFound => new NotFoundObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            }),

            // 400 Bad Request
            ErrorCodes.InvalidAmount or
            ErrorCodes.InvalidCurrency or
            ErrorCodes.InvalidPaymentMethod or
            ErrorCodes.InvalidProvider or
            ErrorCodes.InvalidMerchantId or
            ErrorCodes.InvalidOrderId or
            ErrorCodes.InvalidProjectCode or
            ErrorCodes.InvalidIdempotencyKey or
            ErrorCodes.PaymentAlreadyCompleted or
            ErrorCodes.PaymentAlreadyFailed or
            ErrorCodes.PaymentAlreadyRefunded or
            ErrorCodes.PaymentNotCompleted or
            ErrorCodes.CannotRefundNonCompletedPayment or
            ErrorCodes.CannotFailCompletedPayment or
            ErrorCodes.InvalidPaymentStatus => new BadRequestObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            }),

            // 409 Conflict
            ErrorCodes.IdempotencyKeyMismatch => new ConflictObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            }),

            // 502 Bad Gateway
            ErrorCodes.ProviderError or
            ErrorCodes.ProviderTimeout or
            ErrorCodes.ProviderUnavailable => new ObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            })
            {
                StatusCode = 502
            },

            // 500 Internal Server Error
            _ => new ObjectResult(new { 
                error = result.Error.Code, 
                message = result.Error.Message 
            })
            {
                StatusCode = 500
            }
        };
    }
}

