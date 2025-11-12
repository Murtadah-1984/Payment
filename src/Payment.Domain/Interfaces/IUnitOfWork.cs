namespace Payment.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IPaymentRepository Payments { get; }
    IIdempotentRequestRepository IdempotentRequests { get; }
    IAuditLogRepository AuditLogs { get; } // Audit Logging #7
    IOutboxMessageRepository OutboxMessages { get; } // Outbox Pattern #12
    IWebhookDeliveryRepository WebhookDeliveries { get; } // Webhook Retry Mechanism #20
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

