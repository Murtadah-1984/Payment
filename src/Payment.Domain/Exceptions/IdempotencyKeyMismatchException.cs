namespace Payment.Domain.Exceptions;

/// <summary>
/// Exception thrown when an idempotency key is reused with different request data.
/// This prevents accidental key reuse that could lead to incorrect payment processing.
/// </summary>
public class IdempotencyKeyMismatchException : Exception
{
    public IdempotencyKeyMismatchException(string message) : base(message)
    {
    }

    public IdempotencyKeyMismatchException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}


