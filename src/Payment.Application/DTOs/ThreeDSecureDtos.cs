namespace Payment.Application.DTOs;

/// <summary>
/// DTO for initiating a 3D Secure authentication flow.
/// Note: PaymentId is provided in the route parameter, not in the request body.
/// </summary>
public sealed record InitiateThreeDSecureDto(
    string ReturnUrl);

/// <summary>
/// DTO representing a 3D Secure challenge response.
/// </summary>
public sealed record ThreeDSecureChallengeDto(
    string AcsUrl,
    string Pareq,
    string Md,
    string TermUrl,
    string Version);

/// <summary>
/// DTO for completing a 3D Secure authentication flow.
/// Note: PaymentId is provided in the route parameter, not in the request body.
/// </summary>
public sealed record CompleteThreeDSecureDto(
    string Pareq,
    string Ares,
    string Md);

/// <summary>
/// DTO representing the result of a 3D Secure authentication.
/// </summary>
public sealed record ThreeDSecureResultDto(
    bool Authenticated,
    string? Cavv,
    string? Eci,
    string? Xid,
    string? Version,
    string? FailureReason);

/// <summary>
/// DTO for 3D Secure status information.
/// </summary>
public sealed record ThreeDSecureStatusDto(
    string Status,
    string? Cavv,
    string? Eci,
    string? Xid,
    string? Version);

