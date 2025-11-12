using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;

namespace Payment.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(o => o.EventType)
            .HasColumnName("EventType")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(o => o.Payload)
            .HasColumnName("Payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(o => o.Topic)
            .HasColumnName("Topic")
            .HasMaxLength(255);

        builder.Property(o => o.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(o => o.ProcessedAt)
            .HasColumnName("ProcessedAt");

        builder.Property(o => o.RetryCount)
            .HasColumnName("RetryCount")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(o => o.Error)
            .HasColumnName("Error")
            .HasMaxLength(2000);

        // Index for efficient querying of pending messages
        builder.HasIndex(o => new { o.ProcessedAt, o.CreatedAt })
            .HasDatabaseName("IX_OutboxMessages_Pending");

        // Index for retry count filtering
        builder.HasIndex(o => o.RetryCount)
            .HasDatabaseName("IX_OutboxMessages_RetryCount");
    }
}

