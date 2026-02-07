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
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public bool IsActive => CancelledAt == null;

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

    public void UpdateCompany(string? companyName, string? displayName)
    {
        if (companyName != null)
        {
            if (string.IsNullOrWhiteSpace(companyName))
                throw new ArgumentException("Company name cannot be empty.", nameof(companyName));
            CompanyName = companyName.Trim();
        }
        if (displayName != null)
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applica aggiornamento parziale da comando (solo campi valorizzati).
    /// </summary>
    public void ApplyUpdate(Contracts.Commands.Customers.UpdateCustomer command)
    {
        if (CancelledAt.HasValue)
            throw new InvalidOperationException("Cannot update a cancelled customer.");

        if (command.CompanyName != null) CompanyName = string.IsNullOrWhiteSpace(command.CompanyName) ? CompanyName : command.CompanyName.Trim();
        if (command.DisplayName != null) DisplayName = string.IsNullOrWhiteSpace(command.DisplayName) ? null : command.DisplayName.Trim();
        if (command.Email != null)
        {
            if (string.IsNullOrWhiteSpace(command.Email))
                throw new ArgumentException("Email cannot be empty.", nameof(command.Email));
            Email = command.Email.Trim();
        }
        if (command.Phone != null) Phone = string.IsNullOrWhiteSpace(command.Phone) ? null : command.Phone.Trim();
        if (command.TaxId != null) TaxId = string.IsNullOrWhiteSpace(command.TaxId) ? null : command.TaxId.Trim();
        if (command.VatNumber != null) VatNumber = string.IsNullOrWhiteSpace(command.VatNumber) ? null : command.VatNumber.Trim();
        if (command.BillingAddress != null) BillingAddress = PostalAddress.FromContract(command.BillingAddress);
        if (command.ShippingAddress != null) ShippingAddress = PostalAddress.FromContract(command.ShippingAddress);
        if (command.PreferredLanguage != null && !string.IsNullOrWhiteSpace(command.PreferredLanguage))
            PreferredLanguage = command.PreferredLanguage.Trim();
        if (command.PreferredCurrency != null && !string.IsNullOrWhiteSpace(command.PreferredCurrency))
            PreferredCurrency = command.PreferredCurrency.Trim();
        if (command.Notes != null) Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        if (CancelledAt.HasValue)
            return; // Idempotent: already cancelled
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.", nameof(reason));
        CancellationReason = reason.Trim();
        CancelledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
