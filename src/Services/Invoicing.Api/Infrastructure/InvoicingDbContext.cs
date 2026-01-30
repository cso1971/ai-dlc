using Microsoft.EntityFrameworkCore;

namespace Invoicing.Api.Infrastructure;

public class InvoicingDbContext : DbContext
{
    public InvoicingDbContext(DbContextOptions<InvoicingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Use separate schema for this bounded context
        modelBuilder.HasDefaultSchema("invoicing");
        
        base.OnModelCreating(modelBuilder);
    }
}
