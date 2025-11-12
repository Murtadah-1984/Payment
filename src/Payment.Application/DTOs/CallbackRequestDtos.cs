namespace Payment.Application.DTOs;

/// <summary>
/// Request DTOs for payment callbacks/webhooks.
/// Moved from controller to Application layer following Clean Architecture.
/// </summary>

public sealed record ProcessPaymentRequest(string TransactionId);

public sealed record FailPaymentRequest(string Reason);

public sealed record RefundPaymentRequest(string RefundTransactionId);

public sealed record ZainCashCallbackRequest(string? Token);

public sealed record FibCallbackRequest(string? PaymentId);

public sealed record TelrCallbackRequest(string? OrderId);

