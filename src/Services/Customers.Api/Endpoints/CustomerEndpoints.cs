using Contracts.Commands.Customers;
using Contracts.ValueObjects.Customers;
using Customers.Api.Domain;
using Customers.Api.Services;
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
            .WithDescription("Creates a new customer with company and contact details (DDD Customer aggregate). Persisted to PostgreSQL schema 'customers'.")
            .Produces<CustomerResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> CreateCustomer(
        CreateCustomerRequest request,
        CustomerService customerService,
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
            BillingAddress = request.BillingAddress != null ? MapToContractAddress(request.BillingAddress) : null,
            ShippingAddress = request.ShippingAddress != null ? MapToContractAddress(request.ShippingAddress) : null,
            PreferredLanguage = request.PreferredLanguage,
            PreferredCurrency = request.PreferredCurrency,
            Notes = request.Notes
        };

        try
        {
            var customer = await customerService.CreateCustomerAsync(command, cancellationToken);
            var body = MapToResponse(customer);
            return Results.Created($"/api/customers/{customer.Id}", body);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static Contracts.ValueObjects.Customers.PostalAddress MapToContractAddress(PostalAddressDto dto) => new()
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

    private static CustomerResponse MapToResponse(Customer customer) => new()
    {
        Id = customer.Id,
        CompanyName = customer.CompanyName,
        DisplayName = customer.DisplayName,
        Email = customer.Email,
        Phone = customer.Phone,
        TaxId = customer.TaxId,
        VatNumber = customer.VatNumber,
        BillingAddress = customer.BillingAddress != null ? MapToDto(customer.BillingAddress) : null,
        ShippingAddress = customer.ShippingAddress != null ? MapToDto(customer.ShippingAddress) : null,
        PreferredLanguage = customer.PreferredLanguage,
        PreferredCurrency = customer.PreferredCurrency,
        Notes = customer.Notes,
        CreatedAt = customer.CreatedAt
    };

    private static PostalAddressDto MapToDto(Customers.Api.Domain.PostalAddress a) => new()
    {
        RecipientName = a.RecipientName,
        AddressLine1 = a.AddressLine1,
        AddressLine2 = a.AddressLine2,
        City = a.City,
        StateOrProvince = a.StateOrProvince,
        PostalCode = a.PostalCode,
        CountryCode = a.CountryCode,
        PhoneNumber = a.PhoneNumber,
        Notes = a.Notes
    };
}
