using System.Text.Json.Serialization;

namespace AI.Processor.Clients;

public class ProjectionStatsResponse
{
    [JsonPropertyName("totalOrders")]
    public int TotalOrders { get; init; }

    [JsonPropertyName("lastUpdated")]
    public string? LastUpdated { get; init; }

    [JsonPropertyName("status")]
    public Dictionary<string, DimensionStats> Status { get; init; } = new();

    [JsonPropertyName("currency")]
    public Dictionary<string, DimensionStats> Currency { get; init; } = new();

    [JsonPropertyName("customer-ref")]
    public Dictionary<string, DimensionStats> CustomerRef { get; init; } = new();

    [JsonPropertyName("shipping-method")]
    public Dictionary<string, DimensionStats> ShippingMethod { get; init; } = new();

    [JsonPropertyName("created-month")]
    public Dictionary<string, DimensionStats> CreatedMonth { get; init; } = new();

    [JsonPropertyName("created-year")]
    public Dictionary<string, DimensionStats> CreatedYear { get; init; } = new();

    [JsonPropertyName("delivered-month")]
    public Dictionary<string, DimensionStats> DeliveredMonth { get; init; } = new();

    [JsonPropertyName("delivered-year")]
    public Dictionary<string, DimensionStats> DeliveredYear { get; init; } = new();

    [JsonPropertyName("product")]
    public Dictionary<string, DimensionStats> Product { get; init; } = new();
}

public class DimensionStats
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; init; }

    [JsonPropertyName("grandTotal")]
    public decimal GrandTotal { get; init; }
}
