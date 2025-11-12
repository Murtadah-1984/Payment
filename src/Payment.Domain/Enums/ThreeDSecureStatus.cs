namespace Payment.Domain.Enums;

/// <summary>
/// Represents the status of a 3D Secure authentication flow.
/// </summary>
public enum ThreeDSecureStatus
{
    /// <summary>
    /// 3DS authentication not required or not initiated
    /// </summary>
    NotRequired = 0,

    /// <summary>
    /// 3DS authentication is required and pending
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 3DS authentication challenge initiated, waiting for user response
    /// </summary>
    ChallengeRequired = 2,

    /// <summary>
    /// 3DS authentication completed successfully
    /// </summary>
    Authenticated = 3,

    /// <summary>
    /// 3DS authentication failed
    /// </summary>
    Failed = 4,

    /// <summary>
    /// 3DS authentication was skipped (e.g., frictionless flow)
    /// </summary>
    Skipped = 5
}

