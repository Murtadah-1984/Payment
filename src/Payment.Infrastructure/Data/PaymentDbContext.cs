using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.Interfaces;
using Payment.Domain.ValueObjects;
using Payment.Infrastructure.Data.Configurations;
using Payment.Infrastructure.Data.Converters;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Infrastructure.Data;

public class PaymentDbContext : DbContext
{
    private readonly IDataEncryptionService? _encryptionService;

    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public PaymentDbContext(
        DbContextOptions<PaymentDbContext> options,
        IDataEncryptionService? encryptionService = null) : base(options)
    {
        _encryptionService = encryptionService;
    }

    public DbSet<PaymentEntity> Payments { get; set; } = null!;
    public DbSet<IdempotentRequest> IdempotentRequests { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<PaymentSplit> PaymentSplits { get; set; } = null!;
    public DbSet<Refund> Refunds { get; set; } = null!;
    public DbSet<RefundAuditLog> RefundAuditLogs { get; set; } = null!;
    public DbSet<Domain.Entities.ReportMetadata> ReportMetadata { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; } = null!;
    public DbSet<RevokedCredential> RevokedCredentials { get; set; } = null!;
    public DbSet<SecurityIncident> SecurityIncidents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Set encryption service for metadata converter (if available)
        if (_encryptionService != null)
        {
            MetadataEncryptionConverter.SetEncryptionService(_encryptionService);
        }

        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotentRequestConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.PaymentSplitConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.RefundConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.RefundAuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ReportMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.WebhookDeliveryConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.RevokedCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SecurityIncidentConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}

