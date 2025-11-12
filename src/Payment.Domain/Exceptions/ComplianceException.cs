namespace Payment.Domain.Exceptions;

/// <summary>
/// Exception thrown when a payment transaction violates regulatory compliance rules.
/// </summary>
public class ComplianceException : Exception
{
    public string CountryCode { get; }
    public string RegulationName { get; }

    public ComplianceException(string message, string countryCode, string regulationName)
        : base(message)
    {
        CountryCode = countryCode ?? throw new ArgumentNullException(nameof(countryCode));
        RegulationName = regulationName ?? throw new ArgumentNullException(nameof(regulationName));
    }

    public ComplianceException(string message, string countryCode, string regulationName, Exception innerException)
        : base(message, innerException)
    {
        CountryCode = countryCode ?? throw new ArgumentNullException(nameof(countryCode));
        RegulationName = regulationName ?? throw new ArgumentNullException(nameof(regulationName));
    }
}

