namespace Payment.Domain.Enums;

/// <summary>
/// Represents strategies for containing security incidents.
/// Used to determine appropriate containment actions for security threats.
/// </summary>
public enum ContainmentStrategy
{
    /// <summary>
    /// Isolate the affected Kubernetes pod.
    /// </summary>
    IsolatePod = 0,

    /// <summary>
    /// Block the source IP address.
    /// </summary>
    BlockIpAddress = 1,

    /// <summary>
    /// Revoke compromised credentials.
    /// </summary>
    RevokeCredentials = 2,

    /// <summary>
    /// Disable a specific feature or endpoint.
    /// </summary>
    DisableFeature = 3,

    /// <summary>
    /// Scale down the affected service.
    /// </summary>
    ScaleDown = 4,

    /// <summary>
    /// Isolate the affected service from the network.
    /// </summary>
    NetworkIsolation = 5
}

