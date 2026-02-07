using Contracts.ValueObjects.Customers;

namespace Contracts.Commands.Customers;

public record UpdateCustomer
{
    public Guid CustomerId { get; init; }
    public string? CompanyName { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? TaxId { get; init; }
    public string? VatNumber { get; init; }
    public PostalAddress? BillingAddress { get; init; }
    public PostalAddress? ShippingAddress { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? PreferredCurrency { get; init; }
    public string? Notes { get; init; }
}
