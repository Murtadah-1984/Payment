using Payment.Domain.Entities;

namespace Payment.Domain.Interfaces;

/// <summary>
/// Repository interface for idempotent request operations.
/// Follows Interface Segregation Principle - focused on idempotency concerns.
/// </summary>
public interface IIdempotentRequestRepository
{
    Task<IdempotentRequest?> GetByKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IdempotentRequest> AddAsync(IdempotentRequest entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<IdempotentRequest>> GetExpiredAsync(DateTime beforeDate, CancellationToken cancellationToken = default);
    Task DeleteAsync(IdempotentRequest entity, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredAsync(DateTime beforeDate, CancellationToken cancellationToken = default);
}


