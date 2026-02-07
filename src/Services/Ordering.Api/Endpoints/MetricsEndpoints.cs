using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ordering.Api.Endpoints;

public static class MetricsEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static void MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metrics")
            .WithTags("Metrics")
            .RequireCors("AllowFrontend");

        group.MapGet("/rabbitmq", GetRabbitMQQueueMetrics);
    }

    private static async Task<IResult> GetRabbitMQQueueMetrics(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        var managementUrl = $"http://{host.TrimEnd('/')}:15672";

        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(managementUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}")));
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            // GET /api/queues/%2F (vhost "/")
            var response = await client.GetAsync("api/queues/%2F", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var queues = JsonSerializer.Deserialize<List<RabbitMQQueueDto>>(json, JsonOptions) ?? new List<RabbitMQQueueDto>();

            static int QueueTotal(RabbitMQQueueDto q) =>
                q.Messages > 0 ? q.Messages : (q.MessagesReady + q.MessagesUnacknowledged);

            var totalMessages = queues.Sum(QueueTotal);
            var queueSummaries = queues
                .Select(q => new QueueSummaryDto(q.Name, QueueTotal(q)))
                .OrderByDescending(q => q.Messages)
                .ToList();

            return Results.Ok(new RabbitMQMetricsResponse(totalMessages, queueSummaries));
        }
        catch (HttpRequestException ex)
        {
            return Results.Json(new { Error = "RabbitMQ Management API unreachable", Detail = ex.Message }, statusCode: 503);
        }
        catch (TaskCanceledException)
        {
            return Results.Json(new { Error = "Timeout calling RabbitMQ Management API" }, statusCode: 504);
        }
    }

    private sealed class RabbitMQQueueDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("messages")]
        public int Messages { get; set; }
        [JsonPropertyName("messages_ready")]
        public int MessagesReady { get; set; }
        [JsonPropertyName("messages_unacknowledged")]
        public int MessagesUnacknowledged { get; set; }
    }

    public record QueueSummaryDto(string Name, int Messages);

    public record RabbitMQMetricsResponse(int TotalMessages, List<QueueSummaryDto> Queues);
}
