namespace Payment.Domain.Common;

/// <summary>
/// Domain-specific error codes for the Result pattern.
/// These codes are used to identify specific error types and map them to appropriate HTTP status codes.
/// </summary>
public static class ErrorCodes
{
    // Payment not found errors (404)
    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string PaymentByOrderIdNotFound = "PAYMENT_BY_ORDER_ID_NOT_FOUND";
    
    // Validation errors (400)
    public const string InvalidAmount = "INVALID_AMOUNT";
    public const string InvalidCurrency = "INVALID_CURRENCY";
    public const string InvalidPaymentMethod = "INVALID_PAYMENT_METHOD";
    public const string InvalidProvider = "INVALID_PROVIDER";
    public const string InvalidMerchantId = "INVALID_MERCHANT_ID";
    public const string InvalidOrderId = "INVALID_ORDER_ID";
    public const string InvalidProjectCode = "INVALID_PROJECT_CODE";
    public const string InvalidIdempotencyKey = "INVALID_IDEMPOTENCY_KEY";
    
    // Business logic errors (400)
    public const string PaymentAlreadyCompleted = "PAYMENT_ALREADY_COMPLETED";
    public const string PaymentAlreadyFailed = "PAYMENT_ALREADY_FAILED";
    public const string PaymentAlreadyRefunded = "PAYMENT_ALREADY_REFUNDED";
    public const string PaymentNotCompleted = "PAYMENT_NOT_COMPLETED";
    public const string CannotRefundNonCompletedPayment = "CANNOT_REFUND_NON_COMPLETED_PAYMENT";
    public const string CannotFailCompletedPayment = "CANNOT_FAIL_COMPLETED_PAYMENT";
    public const string InvalidPaymentStatus = "INVALID_PAYMENT_STATUS";
    
    // Idempotency errors (409)
    public const string IdempotencyKeyMismatch = "IDEMPOTENCY_KEY_MISMATCH";
    
    // Provider errors (502)
    public const string ProviderError = "PROVIDER_ERROR";
    public const string ProviderTimeout = "PROVIDER_TIMEOUT";
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
    
    // Internal errors (500)
    public const string InternalError = "INTERNAL_ERROR";
    public const string DatabaseError = "DATABASE_ERROR";
}

