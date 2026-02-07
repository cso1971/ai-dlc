namespace Customers.Api.Endpoints;

// Request DTOs
public record CreateCustomerRequest
{
    public string CompanyName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? TaxId { get; init; }
    public string? VatNumber { get; init; }
    public PostalAddressDto? BillingAddress { get; init; }
    public PostalAddressDto? ShippingAddress { get; init; }
    public string PreferredLanguage { get; init; } = "en";
    public string PreferredCurrency { get; init; } = "EUR";
    public string? Notes { get; init; }
}

/// <summary>Partial update: only set properties you want to change.</summary>
public record UpdateCustomerRequest
{
    public string? CompanyName { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? TaxId { get; init; }
    public string? VatNumber { get; init; }
    public PostalAddressDto? BillingAddress { get; init; }
    public PostalAddressDto? ShippingAddress { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? PreferredCurrency { get; init; }
    public string? Notes { get; init; }
}

public record CancelCustomerRequest
{
    public string CancellationReason { get; init; } = string.Empty;
}

public record PostalAddressDto
{
    public string RecipientName { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string? StateOrProvince { get; init; }
    public string PostalCode { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? Notes { get; init; }
}

// Response DTOs
public record CustomerResponse
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? TaxId { get; init; }
    public string? VatNumber { get; init; }
    public PostalAddressDto? BillingAddress { get; init; }
    public PostalAddressDto? ShippingAddress { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public string PreferredCurrency { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public bool IsActive { get; init; }
}
