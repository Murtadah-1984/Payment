using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;
using System.Text.Json;

namespace Payment.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for AuditLog entity (Audit Logging #7).
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("Id");

        builder.Property(a => a.UserId)
            .HasColumnName("UserId")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnName("Action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasColumnName("EntityType")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnName("EntityId")
            .IsRequired();

        builder.Property(a => a.IpAddress)
            .HasColumnName("IpAddress")
            .HasMaxLength(50);

        builder.Property(a => a.UserAgent)
            .HasColumnName("UserAgent")
            .HasMaxLength(500);

        // Store Changes dictionary as JSON
        builder.Property(a => a.Changes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
            .HasColumnName("Changes")
            .HasColumnType("jsonb");

        builder.Property(a => a.Timestamp)
            .HasColumnName("Timestamp")
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_AuditLogs_UserId");

        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("IX_AuditLogs_Entity");

        builder.HasIndex(a => a.Action)
            .HasDatabaseName("IX_AuditLogs_Action");

        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("IX_AuditLogs_Timestamp");

        // Additional indexes for querying (Audit Log Querying #6)
        builder.HasIndex(a => a.IpAddress)
            .HasDatabaseName("IX_AuditLogs_IpAddress");

        // Composite index for time range queries with user filtering
        builder.HasIndex(a => new { a.Timestamp, a.UserId })
            .HasDatabaseName("IX_AuditLogs_Timestamp_UserId");

        // Composite index for time range queries with IP filtering
        builder.HasIndex(a => new { a.Timestamp, a.IpAddress })
            .HasDatabaseName("IX_AuditLogs_Timestamp_IpAddress");
    }
}

