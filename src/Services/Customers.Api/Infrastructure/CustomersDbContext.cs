using Microsoft.EntityFrameworkCore;

namespace Customers.Api.Infrastructure;

public class CustomersDbContext : DbContext
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Use separate schema for this bounded context
        modelBuilder.HasDefaultSchema("customers");
        
        base.OnModelCreating(modelBuilder);
    }
}
