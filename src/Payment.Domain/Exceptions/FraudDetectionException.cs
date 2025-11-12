using Payment.Domain.Interfaces;

namespace Payment.Domain.Exceptions;

/// <summary>
/// Exception thrown when fraud detection blocks a payment due to high risk.
/// </summary>
public class FraudDetectionException : Exception
{
    public FraudCheckResult FraudResult { get; }

    public FraudDetectionException(string message, FraudCheckResult fraudResult)
        : base(message)
    {
        FraudResult = fraudResult ?? throw new ArgumentNullException(nameof(fraudResult));
    }

    public FraudDetectionException(string message, FraudCheckResult fraudResult, Exception innerException)
        : base(message, innerException)
    {
        FraudResult = fraudResult ?? throw new ArgumentNullException(nameof(fraudResult));
    }
}

