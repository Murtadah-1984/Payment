using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Payment.Infrastructure.Data;

/// <summary>
/// Design-time factory for PaymentDbContext.
/// Enables EF Core migrations and tooling to create DbContext instances.
/// </summary>
public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        // Default connection string for design-time operations
        // In production, this will be overridden by the actual configuration
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=PaymentDb;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        // Design-time factory doesn't need encryption service
        return new PaymentDbContext(optionsBuilder.Options);
    }
}

