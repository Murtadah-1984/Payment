using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Data;
using IdempotentRequestEntity = Payment.Domain.Entities.IdempotentRequest;

namespace Payment.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for idempotent request operations.
/// Follows Repository Pattern - abstracts data access for idempotency tracking.
/// </summary>
public class IdempotentRequestRepository : IIdempotentRequestRepository
{
    private readonly PaymentDbContext _context;

    public IdempotentRequestRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<IdempotentRequestEntity?> GetByKeyAsync(
        string idempotencyKey, 
        CancellationToken cancellationToken = default)
    {
        return await _context.IdempotentRequests
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IdempotentRequestEntity> AddAsync(
        IdempotentRequestEntity entity, 
        CancellationToken cancellationToken = default)
    {
        await _context.IdempotentRequests.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<IEnumerable<IdempotentRequestEntity>> GetExpiredAsync(
        DateTime beforeDate, 
        CancellationToken cancellationToken = default)
    {
        return await _context.IdempotentRequests
            .Where(r => r.ExpiresAt < beforeDate)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(
        IdempotentRequestEntity entity, 
        CancellationToken cancellationToken = default)
    {
        _context.IdempotentRequests.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<int> DeleteExpiredAsync(
        DateTime beforeDate, 
        CancellationToken cancellationToken = default)
    {
        var expired = await GetExpiredAsync(beforeDate, cancellationToken);
        _context.IdempotentRequests.RemoveRange(expired);
        return expired.Count();
    }
}


