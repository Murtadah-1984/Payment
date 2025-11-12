namespace Payment.Application.DTOs;

/// <summary>
/// Represents a remediation action for security incident response.
/// Immutable record following DDD value object pattern.
/// </summary>
public sealed record RemediationAction(
    string Action,
    string Description,
    string Priority,
    string? EstimatedTime = null)
{
    public static RemediationAction RevokeCredentials(string credentialId) =>
        new(
            Action: "RevokeCredentials",
            Description: $"Revoke compromised credential: {credentialId}",
            Priority: "High",
            EstimatedTime: "Immediate");

    public static RemediationAction BlockIpAddress(string ipAddress) =>
        new(
            Action: "BlockIpAddress",
            Description: $"Block IP address: {ipAddress}",
            Priority: "High",
            EstimatedTime: "5 minutes");

    public static RemediationAction IsolatePod(string podName) =>
        new(
            Action: "IsolatePod",
            Description: $"Isolate Kubernetes pod: {podName}",
            Priority: "Critical",
            EstimatedTime: "Immediate");

    public static RemediationAction DisableFeature(string featureName) =>
        new(
            Action: "DisableFeature",
            Description: $"Disable feature: {featureName}",
            Priority: "Medium",
            EstimatedTime: "10 minutes");

    public static RemediationAction NotifySecurityTeam(string message) =>
        new(
            Action: "NotifySecurityTeam",
            Description: message,
            Priority: "High",
            EstimatedTime: "Immediate");
}

