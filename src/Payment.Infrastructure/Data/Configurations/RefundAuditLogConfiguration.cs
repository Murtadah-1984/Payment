using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Data.Configurations;

public class RefundAuditLogConfiguration : IEntityTypeConfiguration<RefundAuditLog>
{
    public void Configure(EntityTypeBuilder<RefundAuditLog> builder)
    {
        builder.ToTable("RefundAuditLogs");

        builder.HasKey(ral => ral.Id);

        builder.Property(ral => ral.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(ral => ral.PaymentId)
            .HasConversion(
                id => id.Value,
                value => PaymentId.FromGuid(value))
            .HasColumnName("PaymentId")
            .IsRequired();

        builder.Property(ral => ral.Action)
            .HasColumnName("Action")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ral => ral.PerformedBy)
            .HasColumnName("PerformedBy")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ral => ral.Reason)
            .HasColumnName("Reason")
            .HasMaxLength(1000);

        builder.Property(ral => ral.Timestamp)
            .HasColumnName("Timestamp")
            .IsRequired();

        builder.HasIndex(ral => ral.PaymentId);
        builder.HasIndex(ral => ral.Timestamp);
        builder.HasIndex(ral => new { ral.PaymentId, ral.Timestamp });
    }
}

