namespace Payment.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a split rule (collection of split definitions).
/// Validates that percentages total 100%.
/// </summary>
public sealed record SplitRule
{
    private readonly List<SplitDefinition> _definitions;

    public SplitRule(IEnumerable<SplitDefinition> definitions)
    {
        _definitions = definitions?.ToList() ?? throw new ArgumentNullException(nameof(definitions));

        if (_definitions.Count == 0)
        {
            throw new ArgumentException("Split rule must contain at least one definition", nameof(definitions));
        }

        var totalPercentage = _definitions.Sum(d => d.Percentage);
        if (Math.Abs(totalPercentage - 100m) > 0.01m)
        {
            throw new ArgumentException($"Split percentages must total 100%, but total is {totalPercentage}%", nameof(definitions));
        }
    }

    public IReadOnlyList<SplitDefinition> Definitions => _definitions.AsReadOnly();

    public decimal TotalPercentage => _definitions.Sum(d => d.Percentage);
}

/// <summary>
/// Represents a single split definition within a split rule.
/// </summary>
public sealed record SplitDefinition
{
    public string AccountType { get; init; }
    public string AccountIdentifier { get; init; }
    public decimal Percentage { get; init; }

    public SplitDefinition(string accountType, string accountIdentifier, decimal percentage)
    {
        if (string.IsNullOrWhiteSpace(accountType))
        {
            throw new ArgumentException("Account type cannot be null or empty", nameof(accountType));
        }

        if (string.IsNullOrWhiteSpace(accountIdentifier))
        {
            throw new ArgumentException("Account identifier cannot be null or empty", nameof(accountIdentifier));
        }

        if (percentage <= 0 || percentage > 100)
        {
            throw new ArgumentException("Percentage must be between 0 and 100", nameof(percentage));
        }

        AccountType = accountType;
        AccountIdentifier = accountIdentifier;
        Percentage = percentage;
    }
}

