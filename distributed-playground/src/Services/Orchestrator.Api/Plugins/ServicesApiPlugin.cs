using System.ComponentModel;
using Microsoft.SemanticKernel;
using Orchestrator.Api.Clients;

namespace Orchestrator.Api.Plugins;

/// <summary>
/// Semantic Kernel plugin that calls Ordering and Customers APIs via HTTP.
/// </summary>
public class ServicesApiPlugin
{
    private readonly IOrderingApiClient _orderingApi;
    private readonly ICustomersApiClient _customersApi;

    public ServicesApiPlugin(IOrderingApiClient orderingApi, ICustomersApiClient customersApi)
    {
        _orderingApi = orderingApi;
        _customersApi = customersApi;
    }

    [KernelFunction, Description("Get the list of all orders from the Ordering API as JSON.")]
    public async Task<string> GetOrders(CancellationToken cancellationToken = default)
    {
        return await _orderingApi.GetOrdersJsonAsync(cancellationToken);
    }

    [KernelFunction, Description("Get order aggregate statistics (total count, total value per currency) from the Ordering API as JSON.")]
    public async Task<string> GetOrderStats(CancellationToken cancellationToken = default)
    {
        return await _orderingApi.GetOrderStatsJsonAsync(cancellationToken);
    }

    [KernelFunction, Description("Get a single order by its ID (GUID). Returns JSON or empty if not found.")]
    public async Task<string> GetOrderById(
        [Description("The order ID (GUID).")] string orderId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(orderId, out var id))
            return "Invalid order ID format.";
        var json = await _orderingApi.GetOrderByIdJsonAsync(id, cancellationToken);
        return json ?? "Order not found.";
    }

    [KernelFunction, Description("Get the list of all customers from the Customers API as JSON.")]
    public async Task<string> GetCustomers(CancellationToken cancellationToken = default)
    {
        return await _customersApi.GetCustomersJsonAsync(cancellationToken);
    }
}
