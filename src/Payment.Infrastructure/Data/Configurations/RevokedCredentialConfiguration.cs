using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;
using Payment.Domain.Enums;

namespace Payment.Infrastructure.Data.Configurations;

public class RevokedCredentialConfiguration : IEntityTypeConfiguration<RevokedCredential>
{
    public void Configure(EntityTypeBuilder<RevokedCredential> builder)
    {
        builder.ToTable("RevokedCredentials");

        builder.HasKey(rc => rc.CredentialId);

        builder.Property(rc => rc.CredentialId)
            .HasColumnName("CredentialId")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(rc => rc.Type)
            .HasConversion(
                type => type.ToString(),
                value => Enum.Parse<CredentialType>(value))
            .HasColumnName("Type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(rc => rc.RevokedAt)
            .HasColumnName("RevokedAt")
            .IsRequired();

        builder.Property(rc => rc.Reason)
            .HasColumnName("Reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(rc => rc.RevokedBy)
            .HasColumnName("RevokedBy")
            .HasMaxLength(255);

        builder.Property(rc => rc.ExpiresAt)
            .HasColumnName("ExpiresAt");

        builder.HasIndex(rc => rc.RevokedAt)
            .HasDatabaseName("IX_RevokedCredentials_RevokedAt");
        
        builder.HasIndex(rc => rc.Type)
            .HasDatabaseName("IX_RevokedCredentials_Type");
    }
}

