using Payment.Domain.Enums;

namespace Payment.Application.DTOs;

/// <summary>
/// Request DTO for containing security incidents.
/// </summary>
public sealed record ContainmentRequest(
    ContainmentStrategy Strategy,
    string? Reason = null)
{
}

