using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;

namespace Payment.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for ReportMetadata entity.
/// </summary>
public class ReportMetadataConfiguration : IEntityTypeConfiguration<ReportMetadata>
{
    public void Configure(EntityTypeBuilder<ReportMetadata> builder)
    {
        builder.ToTable("ReportMetadata");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(r => r.Year)
            .HasColumnName("Year")
            .IsRequired();

        builder.Property(r => r.Month)
            .HasColumnName("Month")
            .IsRequired();

        builder.Property(r => r.ProjectCode)
            .HasColumnName("ProjectCode")
            .HasMaxLength(100);

        builder.Property(r => r.ReportUrl)
            .HasColumnName("ReportUrl")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.PdfUrl)
            .HasColumnName("PdfUrl")
            .HasMaxLength(500);

        builder.Property(r => r.CsvUrl)
            .HasColumnName("CsvUrl")
            .HasMaxLength(500);

        builder.Property(r => r.GeneratedAtUtc)
            .HasColumnName("GeneratedAtUtc")
            .IsRequired();

        builder.Property(r => r.GeneratedBy)
            .HasColumnName("GeneratedBy")
            .HasMaxLength(255)
            .IsRequired();

        // Unique index to prevent duplicate reports
        builder.HasIndex(r => new { r.Year, r.Month, r.ProjectCode })
            .IsUnique()
            .HasDatabaseName("IX_ReportMetadata_Year_Month_ProjectCode");

        // Index for querying by date range
        builder.HasIndex(r => new { r.Year, r.Month })
            .HasDatabaseName("IX_ReportMetadata_Year_Month");
    }
}

