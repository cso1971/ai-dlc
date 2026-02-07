using Contracts.ValueObjects.Customers;

namespace Contracts.Commands.Customers;

public record CreateCustomer
{
    public string CompanyName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? TaxId { get; init; }
    public string? VatNumber { get; init; }
    public PostalAddress? BillingAddress { get; init; }
    public PostalAddress? ShippingAddress { get; init; }
    public string PreferredLanguage { get; init; } = "en";
    public string PreferredCurrency { get; init; } = "EUR";
    public string? Notes { get; init; }
}
