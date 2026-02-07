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

        // GET /api/customers
        group.MapGet("/", GetAllCustomers)
            .WithName("GetAllCustomers")
            .WithSummary("List all customers")
            .WithDescription("Returns a list of all customers (summary).")
            .Produces<List<CustomerSummaryResponse>>(StatusCodes.Status200OK);

        // GET /api/customers/{id}
        group.MapGet("/{id:guid}", GetCustomerById)
            .WithName("GetCustomerById")
            .WithSummary("Get customer by ID")
            .WithDescription("Returns the full customer details by ID.")
            .Produces<CustomerResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // PUT /api/customers/{id}
        group.MapPut("/{id:guid}", UpdateCustomer)
            .WithName("UpdateCustomer")
            .WithSummary("Update a customer")
            .WithDescription("Partial update: only provided fields are changed. Cannot update a cancelled customer.")
            .Produces<CustomerResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/customers/{id}/cancel
        group.MapPost("/{id:guid}/cancel", CancelCustomer)
            .WithName("CancelCustomer")
            .WithSummary("Cancel a customer (soft delete)")
            .WithDescription("Marks the customer as cancelled with a reason. Idempotent if already cancelled.")
            .Produces<CustomerResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/customers
        group.MapPost("/", CreateCustomer)
            .WithName("CreateCustomer")
            .WithSummary("Create a new customer")
            .WithDescription("Creates a new customer with company and contact details (DDD Customer aggregate). Persisted to PostgreSQL schema 'customers'.")
            .Produces<CustomerResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetAllCustomers(CustomerService customerService, CancellationToken cancellationToken)
    {
        var customers = await customerService.GetAllAsync(cancellationToken);
        var list = customers.Select(c => new CustomerSummaryResponse
        {
            Id = c.Id,
            CompanyName = c.CompanyName,
            DisplayName = c.DisplayName,
            Email = c.Email,
            CreatedAt = c.CreatedAt,
            IsActive = c.IsActive
        }).ToList();
        return Results.Ok(list);
    }

    private static async Task<IResult> GetCustomerById(Guid id, CustomerService customerService, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetByIdAsync(id, cancellationToken);
        if (customer == null)
            return Results.NotFound(new { Message = $"Customer {id} not found" });
        return Results.Ok(MapToResponse(customer));
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id,
        UpdateCustomerRequest request,
        CustomerService customerService,
        CancellationToken cancellationToken)
    {
        var command = new Contracts.Commands.Customers.UpdateCustomer
        {
            CustomerId = id,
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
            var customer = await customerService.UpdateCustomerAsync(command, cancellationToken);
            if (customer == null)
                return Results.NotFound(new { Message = $"Customer {id} not found" });
            return Results.Ok(MapToResponse(customer));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static async Task<IResult> CancelCustomer(
        Guid id,
        CancelCustomerRequest request,
        CustomerService customerService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CancellationReason))
            return Results.BadRequest(new { Message = "CancellationReason is required" });

        var command = new Contracts.Commands.Customers.CancelCustomer
        {
            CustomerId = id,
            CancellationReason = request.CancellationReason
        };

        try
        {
            var customer = await customerService.CancelCustomerAsync(command, cancellationToken);
            if (customer == null)
                return Results.NotFound(new { Message = $"Customer {id} not found" });
            return Results.Ok(MapToResponse(customer));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
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
        CreatedAt = customer.CreatedAt,
        UpdatedAt = customer.UpdatedAt,
        CancelledAt = customer.CancelledAt,
        CancellationReason = customer.CancellationReason,
        IsActive = customer.IsActive
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
