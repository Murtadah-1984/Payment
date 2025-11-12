using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Infrastructure.Data;
using System.Text.Json;

namespace Payment.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext _context;
    private IDbContextTransaction? _transaction;
    private IPaymentRepository? _payments;
    private IIdempotentRequestRepository? _idempotentRequests;
    private IAuditLogRepository? _auditLogs;
    private IOutboxMessageRepository? _outboxMessages;
    private IWebhookDeliveryRepository? _webhookDeliveries;

    public UnitOfWork(PaymentDbContext context)
    {
        _context = context;
    }

    public IPaymentRepository Payments => _payments ??= new PaymentRepository(_context);

    public IIdempotentRequestRepository IdempotentRequests => 
        _idempotentRequests ??= new IdempotentRequestRepository(_context);

    public IAuditLogRepository AuditLogs => _auditLogs ??= new AuditLogRepository(_context);

    public IOutboxMessageRepository OutboxMessages => 
        _outboxMessages ??= new OutboxMessageRepository(_context);

    public IWebhookDeliveryRepository WebhookDeliveries => 
        _webhookDeliveries ??= new WebhookDeliveryRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Get domain events from all entities before saving
        var domainEvents = _context.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        // Save changes first
        var result = await _context.SaveChangesAsync(cancellationToken);

        // Save domain events to outbox in the same transaction
        foreach (var domainEvent in domainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = domainEvent.GetType().Name,
                Payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                Topic = GetTopicForEvent(domainEvent),
                CreatedAt = DateTime.UtcNow
            };

            await OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        }

        // Clear domain events from entities
        _context.ChangeTracker
            .Entries<Entity>()
            .ToList()
            .ForEach(e => e.Entity.ClearDomainEvents());

        // Save outbox messages
        if (domainEvents.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private static string GetTopicForEvent(Domain.Events.IDomainEvent domainEvent)
    {
        // Map domain events to topics
        return domainEvent switch
        {
            Domain.Events.PaymentCompletedEvent => "payment.completed",
            Domain.Events.PaymentFailedEvent => "payment.failed",
            Domain.Events.PaymentProcessingEvent => "payment.processing",
            Domain.Events.PaymentRefundedEvent => "payment.refunded",
            Domain.Events.MonthlyReportGeneratedEvent => "payment.reports.monthly.generated",
            _ => "payment.events"
        };
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}

