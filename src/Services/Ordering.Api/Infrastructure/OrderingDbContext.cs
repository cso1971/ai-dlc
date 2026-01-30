using Microsoft.EntityFrameworkCore;
using Ordering.Api.Domain;

namespace Ordering.Api.Infrastructure;

public class OrderingDbContext : DbContext
{
    public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .ValueGeneratedNever();
            
            entity.Property(e => e.CustomerId)
                .IsRequired();
            
            entity.Property(e => e.CustomerReference)
                .HasMaxLength(100);
            
            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired();
            
            entity.Property(e => e.PaymentTerms)
                .HasMaxLength(50);
            
            entity.Property(e => e.ShippingMethod)
                .HasMaxLength(50);
            
            entity.Property(e => e.Notes)
                .HasMaxLength(2000);
            
            entity.Property(e => e.Status)
                .IsRequired();
            
            entity.Property(e => e.TrackingNumber)
                .HasMaxLength(100);
            
            entity.Property(e => e.Carrier)
                .HasMaxLength(100);
            
            entity.Property(e => e.ReceivedBy)
                .HasMaxLength(200);
            
            entity.Property(e => e.DeliveryNotes)
                .HasMaxLength(1000);
            
            entity.Property(e => e.CancellationReason)
                .HasMaxLength(1000);

            // ShippingAddress as owned entity
            entity.OwnsOne(e => e.ShippingAddress, address =>
            {
                address.Property(a => a.RecipientName)
                    .HasColumnName("shipping_recipient_name")
                    .HasMaxLength(200);
                
                address.Property(a => a.AddressLine1)
                    .HasColumnName("shipping_address_line1")
                    .HasMaxLength(200);
                
                address.Property(a => a.AddressLine2)
                    .HasColumnName("shipping_address_line2")
                    .HasMaxLength(200);
                
                address.Property(a => a.City)
                    .HasColumnName("shipping_city")
                    .HasMaxLength(100);
                
                address.Property(a => a.StateOrProvince)
                    .HasColumnName("shipping_state")
                    .HasMaxLength(100);
                
                address.Property(a => a.PostalCode)
                    .HasColumnName("shipping_postal_code")
                    .HasMaxLength(20);
                
                address.Property(a => a.CountryCode)
                    .HasColumnName("shipping_country_code")
                    .HasMaxLength(3);
                
                address.Property(a => a.PhoneNumber)
                    .HasColumnName("shipping_phone")
                    .HasMaxLength(30);
                
                address.Property(a => a.Notes)
                    .HasColumnName("shipping_notes")
                    .HasMaxLength(500);
            });

            // Relationship with OrderLines
            entity.HasMany(e => e.Lines)
                .WithOne()
                .HasForeignKey("OrderId")
                .OnDelete(DeleteBehavior.Cascade);
            
            // Indexes
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // OrderLine configuration
        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("order_lines");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .ValueGeneratedNever();
            
            entity.Property(e => e.ProductCode)
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasMaxLength(500);
            
            entity.Property(e => e.Quantity)
                .HasPrecision(18, 4)
                .IsRequired();
            
            entity.Property(e => e.UnitOfMeasure)
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.UnitPrice)
                .HasPrecision(18, 4)
                .IsRequired();
            
            entity.Property(e => e.DiscountPercent)
                .HasPrecision(5, 2);
            
            entity.Property(e => e.TaxPercent)
                .HasPrecision(5, 2);

            // Ignore computed properties
            entity.Ignore(e => e.LineTotal);
            entity.Ignore(e => e.TaxAmount);
            entity.Ignore(e => e.LineTotalWithTax);
            
            // Index
            entity.HasIndex("OrderId", nameof(OrderLine.LineNumber));
        });

        base.OnModelCreating(modelBuilder);
    }
}
