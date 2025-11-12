namespace Payment.Application.DTOs;

/// <summary>
/// Represents a split payment rule with system fee and multiple account distributions.
/// </summary>
public sealed record SplitRuleDto(
    decimal SystemFeePercent,
    IReadOnlyCollection<SplitAccountDto> Accounts
);

