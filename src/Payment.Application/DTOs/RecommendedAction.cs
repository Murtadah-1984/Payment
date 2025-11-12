namespace Payment.Application.DTOs;

/// <summary>
/// Represents a recommended action for incident response.
/// Immutable record following DDD value object pattern.
/// </summary>
public sealed record RecommendedAction(
    string Action,
    string Description,
    string Priority,
    string? EstimatedTime = null)
{
    public static RecommendedAction SwitchProvider(string providerName) =>
        new(
            Action: "SwitchProvider",
            Description: $"Switch to alternative payment provider: {providerName}",
            Priority: "High",
            EstimatedTime: "5 minutes");

    public static RecommendedAction RetryPayments() =>
        new(
            Action: "RetryPayments",
            Description: "Retry failed payments after provider recovery",
            Priority: "Medium",
            EstimatedTime: "10 minutes");

    public static RecommendedAction ProcessRefunds() =>
        new(
            Action: "ProcessRefunds",
            Description: "Process automatic refunds for affected payments",
            Priority: "High",
            EstimatedTime: "15 minutes");

    public static RecommendedAction ContactProvider() =>
        new(
            Action: "ContactProvider",
            Description: "Contact payment provider support for resolution",
            Priority: "Medium",
            EstimatedTime: "30 minutes");

    public static RecommendedAction EscalateToTeam(string teamName) =>
        new(
            Action: "EscalateToTeam",
            Description: $"Escalate incident to {teamName} team",
            Priority: "High",
            EstimatedTime: "Immediate");
}

