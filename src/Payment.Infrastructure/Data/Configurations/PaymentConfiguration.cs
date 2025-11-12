using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;
using Payment.Domain.ValueObjects;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => PaymentId.FromGuid(value))
            .HasColumnName("Id");

        builder.Property(p => p.Amount)
            .HasConversion(
                amount => amount.Value,
                value => Amount.FromDecimal(value))
            .HasColumnName("Amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasConversion(
                currency => currency.Code,
                code => Currency.FromCode(code))
            .HasColumnName("Currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(p => p.PaymentMethod)
            .HasConversion(
                method => method.Value,
                value => PaymentMethod.FromString(value))
            .HasColumnName("PaymentMethod")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Provider)
            .HasConversion(
                provider => provider.Name,
                name => PaymentProvider.FromString(name))
            .HasColumnName("Provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.OwnsOne(p => p.SplitPayment, split =>
        {
            split.Property(s => s.SystemShare)
                .HasColumnName("SystemShare")
                .HasPrecision(18, 2);
            split.Property(s => s.OwnerShare)
                .HasColumnName("OwnerShare")
                .HasPrecision(18, 2);
            split.Property(s => s.SystemFeePercent)
                .HasColumnName("SystemFeePercent")
                .HasPrecision(5, 2);
        });

        // Encrypt sensitive metadata at rest (PCI DSS compliance)
        builder.Property(p => p.Metadata)
            .HasConversion(new Converters.MetadataEncryptionConverter())
            .HasColumnName("Metadata")
            .HasColumnType("text"); // Store encrypted data as text (not jsonb since it's encrypted)

        builder.Property(p => p.MerchantId)
            .HasColumnName("MerchantId")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.OrderId)
            .HasColumnName("OrderId")
            .HasMaxLength(255)
            .IsRequired();

        // Indexes for common queries (Database Optimization #10)
        builder.HasIndex(p => p.OrderId)
            .IsUnique()
            .HasDatabaseName("IX_Payments_OrderId");
        
        builder.HasIndex(p => p.MerchantId)
            .HasDatabaseName("IX_Payments_MerchantId");
        
        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_Payments_Status");
        
        builder.HasIndex(p => p.TransactionId)
            .HasDatabaseName("IX_Payments_TransactionId");
        
        // Composite index for common query patterns
        builder.HasIndex(p => new { p.MerchantId, p.Status, p.CreatedAt })
            .HasDatabaseName("IX_Payments_Merchant_Status_Date");
        
        // Timestamp columns for efficient range queries
        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("IX_Payments_CreatedAt");
        
        builder.HasIndex(p => p.UpdatedAt)
            .HasDatabaseName("IX_Payments_UpdatedAt");

        builder.Property(p => p.Status)
            .HasColumnName("Status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.TransactionId)
            .HasColumnName("TransactionId")
            .HasMaxLength(255);

        builder.Property(p => p.FailureReason)
            .HasColumnName("FailureReason")
            .HasMaxLength(1000);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .IsRequired();

        builder.Property(p => p.ProjectCode)
            .HasColumnName("ProjectCode")
            .HasMaxLength(100);

        builder.Property(p => p.SystemFeeAmount)
            .HasColumnName("SystemFeeAmount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.RefundedAt)
            .HasColumnName("RefundedAt");

        // Multi-Currency Settlement (LOW #21)
        builder.Property(p => p.SettlementCurrency)
            .HasConversion(
                currency => currency != null ? currency.Code : null,
                code => !string.IsNullOrWhiteSpace(code) ? Currency.FromCode(code) : null)
            .HasColumnName("SettlementCurrency")
            .HasMaxLength(3);

        builder.Property(p => p.SettlementAmount)
            .HasColumnName("SettlementAmount")
            .HasPrecision(18, 2);

        builder.Property(p => p.ExchangeRate)
            .HasColumnName("ExchangeRate")
            .HasPrecision(18, 6);

        builder.Property(p => p.SettledAt)
            .HasColumnName("SettledAt");

        builder.HasIndex(p => p.ProjectCode)
            .HasDatabaseName("IX_Payments_ProjectCode");

        // Configure CardToken as owned entity (PCI DSS compliance - tokenization)
        builder.OwnsOne(p => p.CardToken, cardToken =>
        {
            cardToken.Property(ct => ct.Token)
                .HasColumnName("CardToken")
                .HasMaxLength(500);
            
            cardToken.Property(ct => ct.Last4Digits)
                .HasColumnName("CardLast4Digits")
                .HasMaxLength(4);
            
            cardToken.Property(ct => ct.CardBrand)
                .HasColumnName("CardBrand")
                .HasMaxLength(50);
        });

        builder.Ignore(p => p.DomainEvents);
    }
}

