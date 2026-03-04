namespace AI.Processor.Clients;

public record CustomerResponse
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? TaxId { get; init; }
    public string? VatNumber { get; init; }
    public CustomerPostalAddressDto? BillingAddress { get; init; }
    public CustomerPostalAddressDto? ShippingAddress { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public string PreferredCurrency { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Text representation of the customer for embedding (RAG).
    /// </summary>
    public string ToTextForEmbedding()
    {
        return $"""
            Customer {Id}
            Company: {CompanyName}
            Display Name: {DisplayName ?? "N/A"}
            Email: {Email}
            Phone: {Phone ?? "N/A"}
            Tax ID: {TaxId ?? "N/A"}
            VAT: {VatNumber ?? "N/A"}
            Language: {PreferredLanguage}
            Currency: {PreferredCurrency}
            Created: {CreatedAt:yyyy-MM-dd HH:mm}

            Billing Address:
            {FormatAddress(BillingAddress)}

            Shipping Address:
            {FormatAddress(ShippingAddress)}

            Notes: {Notes ?? "None"}
            """;
    }

    private static string FormatAddress(CustomerPostalAddressDto? a)
    {
        if (a == null) return "N/A";
        return $"{a.RecipientName}\n{a.AddressLine1} {a.AddressLine2 ?? ""}\n{a.City}, {a.StateOrProvince ?? ""} {a.PostalCode}\n{a.CountryCode}\nPhone: {a.PhoneNumber ?? "N/A"}";
    }
}

public record CustomerPostalAddressDto
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
