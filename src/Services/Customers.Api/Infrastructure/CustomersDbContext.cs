using Microsoft.EntityFrameworkCore;
using Customers.Api.Domain;

namespace Customers.Api.Infrastructure;

public class CustomersDbContext : DbContext
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("customers");

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.CompanyName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(200);

            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(e => e.Phone)
                .HasMaxLength(30);

            entity.Property(e => e.TaxId)
                .HasMaxLength(50);

            entity.Property(e => e.VatNumber)
                .HasMaxLength(50);

            entity.Property(e => e.PreferredLanguage)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.PreferredCurrency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(e => e.Notes)
                .HasMaxLength(2000);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            // BillingAddress as owned entity
            entity.OwnsOne(e => e.BillingAddress, address =>
            {
                address.Property(a => a.RecipientName)
                    .HasColumnName("billing_recipient_name")
                    .HasMaxLength(200);
                address.Property(a => a.AddressLine1)
                    .HasColumnName("billing_address_line1")
                    .HasMaxLength(200);
                address.Property(a => a.AddressLine2)
                    .HasColumnName("billing_address_line2")
                    .HasMaxLength(200);
                address.Property(a => a.City)
                    .HasColumnName("billing_city")
                    .HasMaxLength(100);
                address.Property(a => a.StateOrProvince)
                    .HasColumnName("billing_state")
                    .HasMaxLength(100);
                address.Property(a => a.PostalCode)
                    .HasColumnName("billing_postal_code")
                    .HasMaxLength(20);
                address.Property(a => a.CountryCode)
                    .HasColumnName("billing_country_code")
                    .HasMaxLength(3);
                address.Property(a => a.PhoneNumber)
                    .HasColumnName("billing_phone")
                    .HasMaxLength(30);
                address.Property(a => a.Notes)
                    .HasColumnName("billing_notes")
                    .HasMaxLength(500);
            });

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

            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.CreatedAt);
        });

        base.OnModelCreating(modelBuilder);
    }
}
