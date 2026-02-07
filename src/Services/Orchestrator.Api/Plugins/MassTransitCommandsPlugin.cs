using System.ComponentModel;
using Contracts.Commands.Ordering;
using Contracts.ValueObjects.Ordering;
using MassTransit;
using Microsoft.SemanticKernel;

namespace Orchestrator.Api.Plugins;

/// <summary>
/// Semantic Kernel plugin that sends commands via MassTransit (e.g. create order).
/// </summary>
public class MassTransitCommandsPlugin
{
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public MassTransitCommandsPlugin(ISendEndpointProvider sendEndpointProvider)
    {
        _sendEndpointProvider = sendEndpointProvider;
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
