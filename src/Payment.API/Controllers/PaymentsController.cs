using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Payment.API.Extensions;
using Payment.Application.Commands;
using Payment.Application.DTOs;
using Payment.Application.Queries;
using Payment.Application.Services;
using Payment.Domain.Exceptions;

namespace Payment.API.Controllers;

/// <summary>
/// Payments API Controller.
/// Follows Clean Architecture - thin controller that delegates to Application layer via MediatR.
/// No direct dependencies on Infrastructure layer.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IPaymentProviderFactory _providerFactory;

    public PaymentsController(
        IMediator mediator, 
        ILogger<PaymentsController> logger,
        IPaymentProviderFactory providerFactory)
    {
        _mediator = mediator;
        _logger = logger;
        _providerFactory = providerFactory;
    }

    /// <summary>
    /// Provider Discovery API - Gets available payment providers with optional filtering.
    /// Supports filtering by country, currency, or payment method.
    /// </summary>
    /// <param name="country">ISO 3166-1 alpha-2 country code (e.g., "AE", "IQ", "KW")</param>
    /// <param name="currency">Currency code (e.g., "USD", "AED", "IQD")</param>
    /// <param name="method">Payment method (e.g., "Card", "Wallet")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of payment providers matching the filters</returns>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<PaymentProviderInfoDto>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public async Task<IActionResult> GetProviders(
        [FromQuery] string? country,
        [FromQuery] string? currency,
        [FromQuery] string? method,
        CancellationToken cancellationToken)
    {
        var query = new GetProvidersQuery(country, currency, method);
        var providers = await _mediator.Send(query, cancellationToken);
        return Ok(providers);
    }

    /// <summary>
    /// Gets payment providers by country code (ISO 3166-1 alpha-2).
    /// Returns a list of available payment providers for the specified country.
    /// </summary>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code (e.g., "IQ", "KW", "AE")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of payment providers available for the country</returns>
    [HttpGet("providers/{countryCode}")]
    [ProducesResponseType(typeof(IEnumerable<PaymentProviderInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<PaymentProviderInfoDto>>> GetPaymentProvidersByCountry(
        string countryCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            _logger.LogWarning("Invalid country code provided: {CountryCode}", countryCode);
            return BadRequest(new { error = "Country code must be a valid ISO 3166-1 alpha-2 code (2 characters)" });
        }

        var query = new GetPaymentProvidersByCountryQuery(countryCode.ToUpperInvariant());
        var result = await _mediator.Send(query, cancellationToken);
        
        _logger.LogInformation("Retrieved {Count} payment providers for country {CountryCode}", 
            result.Count, countryCode);
        
        return Ok(result);
    }

    /// <summary>
    /// Creates a new payment
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentDto>> CreatePayment([FromBody] CreatePaymentDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating payment for order {OrderId}, requestId={RequestId}, projectCode={ProjectCode}, idempotencyKey={IdempotencyKey}", 
            dto.OrderId, dto.RequestId, dto.ProjectCode, dto.IdempotencyKey);

        try
        {
            var command = new CreatePaymentCommand(
                dto.RequestId,
                dto.Amount,
                dto.Currency,
                dto.PaymentMethod,
                dto.Provider,
                dto.MerchantId,
                dto.OrderId,
                dto.ProjectCode,
                dto.IdempotencyKey,
                dto.SystemFeePercent,
                dto.SplitRule,
                dto.Metadata,
                dto.CallbackUrl,
                dto.CustomerEmail,
                dto.CustomerPhone,
                dto.NfcToken,
                dto.DeviceId,
                dto.CustomerId);

            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetPaymentById), new { id = result.Id }, result);
        }
        catch (IdempotencyKeyMismatchException ex)
        {
            _logger.LogWarning(ex, "Idempotency key mismatch for key {IdempotencyKey}", dto.IdempotencyKey);
            return Conflict(new { error = ex.Message, idempotencyKey = dto.IdempotencyKey });
        }
        catch (FraudDetectionException ex)
        {
            _logger.LogWarning(ex, "Payment blocked by fraud detection for order {OrderId}", dto.OrderId);
            return BadRequest(new 
            { 
                error = "Payment blocked due to fraud risk",
                message = ex.Message,
                riskLevel = ex.FraudResult.RiskLevel.ToString(),
                riskScore = ex.FraudResult.RiskScore,
                reasons = ex.FraudResult.Reasons
            });
        }
    }

    /// <summary>
    /// Gets a payment by ID
    /// Uses Result pattern for error handling (Result Pattern #16).
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaymentDto>> GetPaymentById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetPaymentByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets a payment by Order ID
    /// </summary>
    [HttpGet("order/{orderId}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentDto>> GetPaymentByOrderId(string orderId, CancellationToken cancellationToken)
    {
        var query = new GetPaymentByOrderIdQuery(orderId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets all payments for a merchant
    /// </summary>
    [HttpGet("merchant/{merchantId}")]
    [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPaymentsByMerchant(
        string merchantId,
        CancellationToken cancellationToken)
    {
        var query = new GetPaymentsByMerchantQuery(merchantId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Processes a payment
    /// </summary>
    [HttpPost("{id}/process")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentDto>> ProcessPayment(
        Guid id,
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ProcessPaymentCommand(id, request.TransactionId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Completes a payment
    /// </summary>
    [HttpPost("{id}/complete")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentDto>> CompletePayment(Guid id, CancellationToken cancellationToken)
    {
        var command = new CompletePaymentCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Fails a payment
    /// Uses Result pattern for error handling (Result Pattern #16).
    /// </summary>
    [HttpPost("{id}/fail")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaymentDto>> FailPayment(
        Guid id,
        [FromBody] FailPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new FailPaymentCommand(id, request.Reason);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Refunds a payment
    /// Uses Result pattern for error handling (Result Pattern #16).
    /// Feature flag: RefundSupport (Feature Flags #17).
    /// </summary>
    [HttpPost("{id}/refund")]
    [FeatureGate("RefundSupport")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PaymentDto>> RefundPayment(
        Guid id,
        [FromBody] RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RefundPaymentCommand(id, request.RefundTransactionId);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// ZainCash payment callback endpoint (called after user completes payment).
    /// Signature validation is enforced by WebhookSignatureValidationMiddleware.
    /// </summary>
    [HttpPost("zaincash/callback")]
    [HttpGet("zaincash/callback")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous] // Required for provider callbacks, but signature validation enforced by middleware
    public async Task<ActionResult> ZainCashCallback([FromQuery] string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("ZainCash callback received without token");
            return BadRequest(new { error = "Token is required" });
        }

        try
        {
            var callbackData = new Dictionary<string, string> { { "token", token } };
            var command = new HandlePaymentCallbackCommand("ZainCash", callbackData);
            var result = await _mediator.Send(command, cancellationToken);

            if (result != null)
            {
                _logger.LogInformation("ZainCash payment callback processed successfully. Payment ID: {PaymentId}",
                    result.Id);
                return Ok(new
                {
                    success = true,
                    paymentId = result.Id,
                    transactionId = result.TransactionId,
                    status = result.Status,
                    message = "Payment verified successfully"
                });
            }
            else
            {
                _logger.LogWarning("ZainCash payment callback verification failed");
                return BadRequest(new
                {
                    success = false,
                    error = "Payment verification failed"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ZainCash callback");
            return StatusCode(500, new { error = "Internal server error processing callback" });
        }
    }

    /// <summary>
    /// FIB payment callback endpoint (called when payment status changes).
    /// Signature validation is enforced by WebhookSignatureValidationMiddleware.
    /// </summary>
    [HttpPost("fib/callback")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous] // Required for provider callbacks, but signature validation enforced by middleware
    public async Task<ActionResult> FibCallback([FromBody] FibCallbackRequest? request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrEmpty(request.PaymentId))
        {
            _logger.LogWarning("FIB callback received without payment ID");
            return BadRequest(new { error = "Payment ID is required" });
        }

        try
        {
            var callbackData = new Dictionary<string, string> { { "paymentId", request.PaymentId } };
            var command = new HandlePaymentCallbackCommand("FIB", callbackData);
            var result = await _mediator.Send(command, cancellationToken);

            if (result != null)
            {
                _logger.LogInformation("FIB payment callback processed successfully. Payment ID: {PaymentId}",
                    result.Id);
                return Ok(new
                {
                    success = true,
                    paymentId = result.Id,
                    transactionId = result.TransactionId,
                    status = result.Status,
                    message = "Payment status updated successfully"
                });
            }
            else
            {
                _logger.LogWarning("FIB payment callback verification failed");
                return Ok(new
                {
                    success = false,
                    paymentId = request.PaymentId,
                    error = "Payment verification failed"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing FIB callback");
            return StatusCode(500, new { error = "Internal server error processing callback" });
        }
    }

    /// <summary>
    /// Telr payment callback endpoint (called after user completes payment).
    /// Signature validation is enforced by WebhookSignatureValidationMiddleware.
    /// </summary>
    [HttpPost("telr/callback")]
    [HttpGet("telr/callback")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [AllowAnonymous] // Required for provider callbacks, but signature validation enforced by middleware
    public async Task<ActionResult> TelrCallback([FromQuery] string? order_id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(order_id))
        {
            _logger.LogWarning("Telr callback received without order ID");
            return BadRequest(new { error = "Order ID is required" });
        }

        try
        {
            var callbackData = new Dictionary<string, string> { { "order_id", order_id } };
            var command = new HandlePaymentCallbackCommand("Telr", callbackData);
            var result = await _mediator.Send(command, cancellationToken);

            if (result != null)
            {
                _logger.LogInformation("Telr payment callback processed successfully. Payment ID: {PaymentId}",
                    result.Id);
                return Ok(new
                {
                    success = true,
                    paymentId = result.Id,
                    orderId = order_id,
                    transactionId = result.TransactionId,
                    status = result.Status,
                    message = "Payment verified successfully"
                });
            }
            else
            {
                _logger.LogWarning("Telr payment callback verification failed");
                return Ok(new
                {
                    success = false,
                    orderId = order_id,
                    error = "Payment verification failed"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telr callback");
            return StatusCode(500, new { error = "Internal server error processing callback" });
        }
    }

    /// <summary>
    /// Initiates 3D Secure authentication for a payment.
    /// Returns a challenge that the client should redirect the user to for authentication.
    /// </summary>
    [HttpPost("{id}/3ds/initiate")]
    [ProducesResponseType(typeof(ThreeDSecureChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ThreeDSecureChallengeDto?>> InitiateThreeDSecure(
        Guid id,
        [FromBody] InitiateThreeDSecureDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initiating 3D Secure for payment {PaymentId}", id);

        try
        {
            var command = new InitiateThreeDSecureCommand(id, request.ReturnUrl);
            var result = await _mediator.Send(command, cancellationToken);

            if (result == null)
            {
                // 3DS not required
                return Ok(new { message = "3D Secure authentication not required for this payment" });
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error initiating 3D Secure for payment {PaymentId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Completes 3D Secure authentication for a payment.
    /// Called after the user completes authentication on the ACS (Access Control Server).
    /// Supports both POST (JSON body) and GET (query parameters) for ACS redirects.
    /// </summary>
    [HttpPost("{id}/3ds/callback")]
    [HttpGet("{id}/3ds/callback")]
    [ProducesResponseType(typeof(ThreeDSecureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous] // Required for ACS callbacks, but should be validated
    public async Task<ActionResult<ThreeDSecureResultDto>> CompleteThreeDSecure(
        Guid id,
        [FromBody] CompleteThreeDSecureDto? requestBody,
        [FromQuery] string? pareq,
        [FromQuery] string? ares,
        [FromQuery] string? md,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing 3D Secure authentication for payment {PaymentId}", id);

        try
        {
            // Support both POST (JSON body) and GET (query parameters)
            string finalPareq;
            string finalAres;
            string finalMd;

            if (requestBody != null && !string.IsNullOrEmpty(requestBody.Pareq))
            {
                // POST request with JSON body
                finalPareq = requestBody.Pareq;
                finalAres = requestBody.Ares;
                finalMd = requestBody.Md;
            }
            else if (!string.IsNullOrEmpty(pareq) && !string.IsNullOrEmpty(ares) && !string.IsNullOrEmpty(md))
            {
                // GET request with query parameters (ACS redirect)
                finalPareq = pareq;
                finalAres = ares;
                finalMd = md;
            }
            else
            {
                _logger.LogWarning("3DS callback for payment {PaymentId} missing required parameters", id);
                return BadRequest(new { error = "Missing required parameters: pareq, ares, and md are required" });
            }

            var command = new CompleteThreeDSecureCommand(
                id,
                finalPareq,
                finalAres,
                finalMd);

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error completing 3D Secure for payment {PaymentId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }
}

