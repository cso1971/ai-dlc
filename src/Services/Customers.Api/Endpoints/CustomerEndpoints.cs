using Contracts.Commands.Customers;
using Contracts.ValueObjects.Customers;
using Customers.Api.Consumers;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Customers.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers");

        // POST /api/customers
        group.MapPost("/", CreateCustomer)
            .WithName("CreateCustomer")
            .WithSummary("Create a new customer")
            .WithDescription("Creates a new customer with company and contact details (DDD Customer aggregate data). Uses MassTransit command; persistence will be added in a later step.")
            .Produces<CustomerResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> CreateCustomer(
        CreateCustomerRequest request,
        IRequestClient<CreateCustomer> requestClient,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomer
        {
            CompanyName = request.CompanyName,
            DisplayName = request.DisplayName,
            Email = request.Email,
            Phone = request.Phone,
            TaxId = request.TaxId,
            VatNumber = request.VatNumber,
            BillingAddress = request.BillingAddress != null ? MapToPostalAddress(request.BillingAddress) : null,
            ShippingAddress = request.ShippingAddress != null ? MapToPostalAddress(request.ShippingAddress) : null,
            PreferredLanguage = request.PreferredLanguage,
            PreferredCurrency = request.PreferredCurrency,
            Notes = request.Notes
        };

        var response = await requestClient.GetResponse<CreateCustomerResponse>(command, cancellationToken);

        if (!response.Message.Success)
            return Results.BadRequest(new { Message = response.Message.ErrorMessage ?? "Create customer failed" });

        var created = response.Message;
        var body = new CustomerResponse
        {
            Id = created.CustomerId,
            CompanyName = request.CompanyName,
            DisplayName = request.DisplayName,
            Email = request.Email,
            Phone = request.Phone,
            TaxId = request.TaxId,
            VatNumber = request.VatNumber,
            BillingAddress = request.BillingAddress,
            ShippingAddress = request.ShippingAddress,
            PreferredLanguage = request.PreferredLanguage,
            PreferredCurrency = request.PreferredCurrency,
            Notes = request.Notes,
            CreatedAt = created.CreatedAt ?? DateTime.UtcNow
        };

        return Results.Created($"/api/customers/{created.CustomerId}", body);
    }

    private static PostalAddress MapToPostalAddress(PostalAddressDto dto) => new()
    {
        RecipientName = dto.RecipientName,
        AddressLine1 = dto.AddressLine1,
        AddressLine2 = dto.AddressLine2,
        City = dto.City,
        StateOrProvince = dto.StateOrProvince,
        PostalCode = dto.PostalCode,
        CountryCode = dto.CountryCode,
        PhoneNumber = dto.PhoneNumber,
        Notes = dto.Notes
    };
}
