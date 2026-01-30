namespace Ordering.Api.Domain;

public class ShippingAddress
{
    public string RecipientName { get; private set; } = string.Empty;
    public string AddressLine1 { get; private set; } = string.Empty;
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? StateOrProvince { get; private set; }
    public string PostalCode { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? Notes { get; private set; }
    
    private ShippingAddress() { } // For EF Core
    
    public static ShippingAddress Create(
        string recipientName,
        string addressLine1,
        string? addressLine2,
        string city,
        string? stateOrProvince,
        string postalCode,
        string countryCode,
        string? phoneNumber = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new ArgumentException("Recipient name is required.", nameof(recipientName));
        
        if (string.IsNullOrWhiteSpace(addressLine1))
            throw new ArgumentException("Address line 1 is required.", nameof(addressLine1));
        
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.", nameof(city));
        
        if (string.IsNullOrWhiteSpace(postalCode))
            throw new ArgumentException("Postal code is required.", nameof(postalCode));
        
        if (string.IsNullOrWhiteSpace(countryCode))
            throw new ArgumentException("Country code is required.", nameof(countryCode));
        
        return new ShippingAddress
        {
            RecipientName = recipientName,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = city,
            StateOrProvince = stateOrProvince,
            PostalCode = postalCode,
            CountryCode = countryCode,
            PhoneNumber = phoneNumber,
            Notes = notes
        };
    }
    
    public static ShippingAddress FromContract(Contracts.ValueObjects.Ordering.ShippingAddress contract)
    {
        return Create(
            contract.RecipientName,
            contract.AddressLine1,
            contract.AddressLine2,
            contract.City,
            contract.StateOrProvince,
            contract.PostalCode,
            contract.CountryCode,
            contract.PhoneNumber,
            contract.Notes);
    }
}
