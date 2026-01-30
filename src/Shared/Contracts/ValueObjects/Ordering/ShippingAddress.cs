namespace Contracts.ValueObjects.Ordering;

public record ShippingAddress
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
