using System.ComponentModel;
using Contracts.Commands.Customers;
using Contracts.Commands.Ordering;
using Contracts.ValueObjects.Ordering;
using MassTransit;
using Microsoft.SemanticKernel;

namespace Orchestrator.Api.Plugins;

/// <summary>
/// Semantic Kernel plugin that sends commands via MassTransit (create order, create customer).
/// </summary>
public class MassTransitCommandsPlugin
{
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public MassTransitCommandsPlugin(ISendEndpointProvider sendEndpointProvider)
    {
        _sendEndpointProvider = sendEndpointProvider;
    }

    [KernelFunction, Description("Create a new customer by sending a CreateCustomer command. Use companyName for the legal name and displayName for a short name (e.g. 'Acme SPA'). Email is required.")]
    public async Task<string> SendCreateCustomer(
        [Description("Company or legal name (e.g. 'Acme SPA').")] string companyName,
        [Description("Short display name (e.g. 'Acme'). Optional, can be same as companyName.")] string? displayName,
        [Description("Email address (required).")] string email,
        [Description("Phone number. Optional.")] string? phone,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "Error: email is required to create a customer.";
        var command = new CreateCustomer
        {
            CompanyName = companyName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? companyName.Trim() : displayName.Trim(),
            Email = email.Trim(),
            Phone = phone?.Trim(),
            PreferredLanguage = "it",
            PreferredCurrency = "EUR"
        };
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri("queue:create-customer"));
        await endpoint.Send(command, cancellationToken);
        return "CreateCustomer command sent successfully. The customer will be created shortly.";
    }

    [KernelFunction, Description("Send a CreateOrder command to the ordering service. CustomerId must be an existing customer GUID. Use one or more line items with product code, description, quantity, unit price.")]
    public async Task<string> SendCreateOrder(
        [Description("Customer ID (GUID) - must exist in Customers API.")] string customerId,
        [Description("Optional customer order reference (e.g. PO number).")] string? customerReference,
        [Description("Product code for the first line item.")] string productCode,
        [Description("Product description for the first line item.")] string description,
        [Description("Quantity.")] int quantity,
        [Description("Unit price.")] decimal unitPrice,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(customerId, out var cid))
            return "Error: customerId must be a valid GUID.";
        var command = new CreateOrder
        {
            CustomerId = cid,
            CustomerReference = customerReference,
            Priority = 3,
            CurrencyCode = "EUR",
            Lines =
            [
                new OrderLineItem
                {
                    ProductCode = productCode,
                    Description = description,
                    Quantity = quantity,
                    UnitOfMeasure = "EA",
                    UnitPrice = unitPrice,
                    DiscountPercent = 0,
                    TaxPercent = 22
                }
            ]
        };
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri("queue:create-order"));
        await endpoint.Send(command, cancellationToken);
        return "CreateOrder command sent successfully.";
    }
}
