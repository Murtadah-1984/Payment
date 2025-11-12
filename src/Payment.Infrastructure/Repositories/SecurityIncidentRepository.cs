using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Data;

namespace Payment.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for security incident operations.
/// Follows Repository Pattern - abstracts data access for security incidents.
/// Stateless by design - suitable for Kubernetes deployment.
/// </summary>
public class SecurityIncidentRepository : ISecurityIncidentRepository
{
    private readonly PaymentDbContext _context;

    public SecurityIncidentRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<SecurityIncident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SecurityIncidents
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<SecurityIncident>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SecurityIncidents.ToListAsync(cancellationToken);
    }

    public async Task<SecurityIncident> AddAsync(SecurityIncident entity, CancellationToken cancellationToken = default)
    {
        await _context.SecurityIncidents.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(SecurityIncident entity, CancellationToken cancellationToken = default)
    {
        _context.SecurityIncidents.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SecurityIncident entity, CancellationToken cancellationToken = default)
    {
        _context.SecurityIncidents.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<SecurityIncident?> GetByIncidentIdAsync(SecurityIncidentId incidentId, CancellationToken cancellationToken = default)
    {
        return await _context.SecurityIncidents
            .FirstOrDefaultAsync(s => s.IncidentId == incidentId.Value, cancellationToken);
    }
}

