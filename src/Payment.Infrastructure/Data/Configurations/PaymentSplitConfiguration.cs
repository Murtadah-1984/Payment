using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;
using Payment.Domain.ValueObjects;

namespace Payment.Infrastructure.Data.Configurations;

public class PaymentSplitConfiguration : IEntityTypeConfiguration<PaymentSplit>
{
    public void Configure(EntityTypeBuilder<PaymentSplit> builder)
    {
        builder.ToTable("PaymentSplits");

        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(ps => ps.PaymentId)
            .HasConversion(
                id => id.Value,
                value => PaymentId.FromGuid(value))
            .HasColumnName("PaymentId")
            .IsRequired();

        builder.Property(ps => ps.AccountType)
            .HasColumnName("AccountType")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ps => ps.AccountIdentifier)
            .HasColumnName("AccountIdentifier")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ps => ps.Percentage)
            .HasColumnName("Percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(ps => ps.Amount)
            .HasColumnName("Amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(ps => ps.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.HasIndex(ps => ps.PaymentId);
        builder.HasIndex(ps => new { ps.PaymentId, ps.AccountType });
    }
}

