using Microsoft.EntityFrameworkCore;

namespace Ordering.Api.Infrastructure;

public class OrderingDbContext : DbContext
{
    public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Use separate schema for this bounded context
        modelBuilder.HasDefaultSchema("ordering");
        
        base.OnModelCreating(modelBuilder);
    }
}
