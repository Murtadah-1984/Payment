namespace Payment.Domain.Enums;

public enum PaymentStatus
{
    Initiated = 0,
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Completed = 3, // Alias for Succeeded (backward compatibility)
    Failed = 4,
    Refunded = 5,
    PartiallyRefunded = 6,
    Cancelled = 7
}

