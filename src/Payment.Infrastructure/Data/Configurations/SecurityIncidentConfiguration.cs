using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;
using Payment.Domain.Enums;

namespace Payment.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for SecurityIncident entity.
/// </summary>
public class SecurityIncidentConfiguration : IEntityTypeConfiguration<SecurityIncident>
{
    public void Configure(EntityTypeBuilder<SecurityIncident> builder)
    {
        builder.ToTable("SecurityIncidents");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("Id");

        builder.Property(s => s.IncidentId)
            .HasColumnName("IncidentId")
            .IsRequired();

        builder.Property(s => s.SecurityEventType)
            .HasColumnName("SecurityEventType")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.SecurityEventTimestamp)
            .HasColumnName("SecurityEventTimestamp")
            .IsRequired();

        builder.Property(s => s.SecurityEventUserId)
            .HasColumnName("SecurityEventUserId")
            .HasMaxLength(255);

        builder.Property(s => s.SecurityEventIpAddress)
            .HasColumnName("SecurityEventIpAddress")
            .HasMaxLength(50);

        builder.Property(s => s.SecurityEventResource)
            .HasColumnName("SecurityEventResource")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.SecurityEventAction)
            .HasColumnName("SecurityEventAction")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.SecurityEventSucceeded)
            .HasColumnName("SecurityEventSucceeded")
            .IsRequired();

        builder.Property(s => s.SecurityEventDetails)
            .HasColumnName("SecurityEventDetails")
            .HasColumnType("text");

        builder.Property(s => s.Severity)
            .HasColumnName("Severity")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.ThreatType)
            .HasColumnName("ThreatType")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.AffectedResources)
            .HasColumnName("AffectedResources")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.CompromisedCredentials)
            .HasColumnName("CompromisedCredentials")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.RecommendedContainment)
            .HasColumnName("RecommendedContainment")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.RemediationActions)
            .HasColumnName("RemediationActions")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(s => s.ContainedAt)
            .HasColumnName("ContainedAt");

        builder.Property(s => s.ContainmentStrategy)
            .HasColumnName("ContainmentStrategy")
            .HasConversion<int?>();

        // Indexes for common queries
        builder.HasIndex(s => s.IncidentId)
            .HasDatabaseName("IX_SecurityIncidents_IncidentId")
            .IsUnique();

        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("IX_SecurityIncidents_CreatedAt");

        builder.HasIndex(s => s.Severity)
            .HasDatabaseName("IX_SecurityIncidents_Severity");

        builder.HasIndex(s => s.SecurityEventType)
            .HasDatabaseName("IX_SecurityIncidents_SecurityEventType");

        builder.HasIndex(s => s.SecurityEventUserId)
            .HasDatabaseName("IX_SecurityIncidents_SecurityEventUserId");
    }
}

