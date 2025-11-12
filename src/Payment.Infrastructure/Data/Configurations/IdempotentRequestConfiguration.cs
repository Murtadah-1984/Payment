using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;

namespace Payment.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for IdempotentRequest entity.
/// Defines database schema, indexes, and constraints.
/// </summary>
public class IdempotentRequestConfiguration : IEntityTypeConfiguration<IdempotentRequest>
{
    public void Configure(EntityTypeBuilder<IdempotentRequest> builder)
    {
        builder.ToTable("IdempotentRequests");

        builder.HasKey(r => r.IdempotencyKey);

        builder.Property(r => r.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.PaymentId)
            .IsRequired();

        builder.Property(r => r.RequestHash)
            .HasMaxLength(64) // SHA-256 produces 64-character hex string
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.ExpiresAt)
            .IsRequired();

        // Index on IdempotencyKey for fast lookups (primary key already provides this)
        builder.HasIndex(r => r.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_IdempotentRequests_IdempotencyKey");

        // Index on ExpiresAt for efficient cleanup queries
        builder.HasIndex(r => r.ExpiresAt)
            .HasDatabaseName("IX_IdempotentRequests_ExpiresAt");

        // Index on PaymentId for reverse lookups
        builder.HasIndex(r => r.PaymentId)
            .HasDatabaseName("IX_IdempotentRequests_PaymentId");
    }
}


