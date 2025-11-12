using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Data.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(r => r.PaymentId)
            .HasConversion(
                id => id.Value,
                value => PaymentId.FromGuid(value))
            .HasColumnName("PaymentId")
            .IsRequired();

        builder.Property(r => r.Amount)
            .HasConversion(
                amount => amount.Value,
                value => Amount.FromDecimal(value))
            .HasColumnName("Amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.Currency)
            .HasConversion(
                currency => currency.Code,
                code => Currency.FromCode(code))
            .HasColumnName("Currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(r => r.Reason)
            .HasColumnName("Reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("Status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.RefundTransactionId)
            .HasColumnName("RefundTransactionId")
            .HasMaxLength(255);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(r => r.ProcessedAt)
            .HasColumnName("ProcessedAt");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .IsRequired();

        builder.HasIndex(r => r.PaymentId);
        builder.HasIndex(r => r.RefundTransactionId);
        builder.HasIndex(r => r.CreatedAt);
    }
}

