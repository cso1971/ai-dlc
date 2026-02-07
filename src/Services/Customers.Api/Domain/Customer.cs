using Contracts.Commands.Customers;

namespace Customers.Api.Domain;

/// <summary>
/// Aggregate root del bounded context Customers. EF: entità principale con OwnsOne per BillingAddress e ShippingAddress.
/// </summary>
public class Customer
{
    public Guid Id { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? TaxId { get; private set; }
    public string? VatNumber { get; private set; }
    public PostalAddress? BillingAddress { get; private set; }
    public PostalAddress? ShippingAddress { get; private set; }
    public string PreferredLanguage { get; private set; } = "en";
    public string PreferredCurrency { get; private set; } = "EUR";
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Customer() { } // Per EF Core

    public static Customer Create(
        string companyName,
        string email,
        string? displayName = null,
        string? phone = null,
        string? taxId = null,
        string? vatNumber = null,
        PostalAddress? billingAddress = null,
        PostalAddress? shippingAddress = null,
        string preferredLanguage = "en",
        string preferredCurrency = "EUR",
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return new Customer
        {
            Id = Guid.NewGuid(),
            CompanyName = companyName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            Email = email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            TaxId = string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim(),
            VatNumber = string.IsNullOrWhiteSpace(vatNumber) ? null : vatNumber.Trim(),
            BillingAddress = billingAddress,
            ShippingAddress = shippingAddress,
            PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? "en" : preferredLanguage.Trim(),
            PreferredCurrency = string.IsNullOrWhiteSpace(preferredCurrency) ? "EUR" : preferredCurrency.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Crea un'istanza del dominio a partire dal comando CreateCustomer (es. da REST o da MassTransit).
    /// </summary>
    public static Customer FromCommand(CreateCustomer command)
    {
        var billing = PostalAddress.FromContract(command.BillingAddress);
        var shipping = PostalAddress.FromContract(command.ShippingAddress);
        return Create(
            command.CompanyName,
            command.Email,
            command.DisplayName,
            command.Phone,
            command.TaxId,
            command.VatNumber,
            billing,
            shipping,
            command.PreferredLanguage,
            command.PreferredCurrency,
            command.Notes);
    }

    public void UpdateContact(string? phone, string? email)
    {
        if (email != null)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty.", nameof(email));
            Email = email.Trim();
        }
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateAddresses(PostalAddress? billingAddress, PostalAddress? shippingAddress)
    {
        BillingAddress = billingAddress;
        ShippingAddress = shippingAddress;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePreferences(string? preferredLanguage, string? preferredCurrency)
    {
        if (!string.IsNullOrWhiteSpace(preferredLanguage))
            PreferredLanguage = preferredLanguage.Trim();
        if (!string.IsNullOrWhiteSpace(preferredCurrency))
            PreferredCurrency = preferredCurrency.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
