using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;

namespace Payment.Infrastructure.Data.Configurations;

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(w => w.PaymentId)
            .HasColumnName("PaymentId")
            .IsRequired();

        builder.Property(w => w.WebhookUrl)
            .HasColumnName("WebhookUrl")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(w => w.EventType)
            .HasColumnName("EventType")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(w => w.Payload)
            .HasColumnName("Payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(w => w.Status)
            .HasColumnName("Status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(w => w.RetryCount)
            .HasColumnName("RetryCount")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(w => w.MaxRetries)
            .HasColumnName("MaxRetries")
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(w => w.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(w => w.NextRetryAt)
            .HasColumnName("NextRetryAt");

        builder.Property(w => w.LastAttemptedAt)
            .HasColumnName("LastAttemptedAt");

        builder.Property(w => w.DeliveredAt)
            .HasColumnName("DeliveredAt");

        builder.Property(w => w.LastError)
            .HasColumnName("LastError")
            .HasMaxLength(2000);

        builder.Property(w => w.LastHttpStatusCode)
            .HasColumnName("LastHttpStatusCode");

        builder.Property(w => w.InitialRetryDelay)
            .HasColumnName("InitialRetryDelay")
            .HasConversion(
                v => v.TotalMilliseconds,
                v => TimeSpan.FromMilliseconds(v))
            .IsRequired();

        // Indexes for efficient querying
        builder.HasIndex(w => new { w.Status, w.NextRetryAt })
            .HasDatabaseName("IX_WebhookDeliveries_PendingRetries");

        builder.HasIndex(w => w.PaymentId)
            .HasDatabaseName("IX_WebhookDeliveries_PaymentId");

        builder.HasIndex(w => w.CreatedAt)
            .HasDatabaseName("IX_WebhookDeliveries_CreatedAt");
    }
}

