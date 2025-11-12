using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payment.Infrastructure.Migrations;

/// <summary>
/// Migration to add additional indexes for audit log querying.
/// Improves query performance for filtering by IpAddress and time range queries.
/// </summary>
public partial class AddAuditLogIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add index on IpAddress for security event queries
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_IpAddress",
            table: "AuditLogs",
            column: "IpAddress");

        // Composite index for time range queries with user filtering
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Timestamp_UserId",
            table: "AuditLogs",
            columns: new[] { "Timestamp", "UserId" });

        // Composite index for time range queries with IP filtering
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Timestamp_IpAddress",
            table: "AuditLogs",
            columns: new[] { "Timestamp", "IpAddress" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AuditLogs_Timestamp_IpAddress",
            table: "AuditLogs");

        migrationBuilder.DropIndex(
            name: "IX_AuditLogs_Timestamp_UserId",
            table: "AuditLogs");

        migrationBuilder.DropIndex(
            name: "IX_AuditLogs_IpAddress",
            table: "AuditLogs");
    }
}

