using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Data;

namespace Payment.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for webhook delivery entities.
/// </summary>
public class WebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly PaymentDbContext _context;

    public WebhookDeliveryRepository(PaymentDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<WebhookDelivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookDeliveries
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<WebhookDelivery>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.WebhookDeliveries.ToListAsync(cancellationToken);
    }

    public async Task<WebhookDelivery> AddAsync(WebhookDelivery entity, CancellationToken cancellationToken = default)
    {
        await _context.WebhookDeliveries.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(WebhookDelivery entity, CancellationToken cancellationToken = default)
    {
        _context.WebhookDeliveries.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(WebhookDelivery entity, CancellationToken cancellationToken = default)
    {
        _context.WebhookDeliveries.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<WebhookDelivery>> GetPendingRetriesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.WebhookDeliveries
            .Where(w => w.Status == WebhookDeliveryStatus.Pending &&
                       w.NextRetryAt.HasValue &&
                       w.NextRetryAt.Value <= now &&
                       w.RetryCount < w.MaxRetries)
            .OrderBy(w => w.NextRetryAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WebhookDelivery>> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookDeliveries
            .Where(w => w.PaymentId == paymentId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WebhookDelivery>> GetFailedDeliveriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.WebhookDeliveries
            .Where(w => w.Status == WebhookDeliveryStatus.Failed ||
                       (w.Status == WebhookDeliveryStatus.Pending && w.RetryCount >= w.MaxRetries))
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

