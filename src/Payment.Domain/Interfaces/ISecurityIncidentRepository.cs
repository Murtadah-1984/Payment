using Payment.Domain.Entities;
using Payment.Domain.ValueObjects;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Repository interface for security incident operations.
/// Follows Repository Pattern - abstracts data access for security incidents.
/// </summary>
public interface ISecurityIncidentRepository : IRepository<SecurityIncident>
{
    Task<SecurityIncident?> GetByIncidentIdAsync(SecurityIncidentId incidentId, CancellationToken cancellationToken = default);
}

