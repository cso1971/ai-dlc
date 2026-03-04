using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Projections.Clients;
using Projections.Consumers;
using Projections.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ===== Redis =====
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IRedisProjectionService, RedisProjectionService>();

// ===== Order API Client with Polly =====
builder.Services.AddHttpClient<IOrderApiClient, OrderApiClient>(client =>
{
    var baseUrl = builder.Configuration["OrderingApi:BaseUrl"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// ===== MassTransit with RabbitMQ =====
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderProjectionConsumer>();

    x.SetKebabCaseEndpointNameFormatter();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqConfig = builder.Configuration.GetSection("RabbitMQ");
        cfg.Host(rabbitMqConfig["Host"] ?? "localhost", "/", h =>
        {
            h.Username(rabbitMqConfig["Username"] ?? "guest");
            h.Password(rabbitMqConfig["Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// ===== OpenTelemetry =====
var otelConfig = builder.Configuration.GetSection("OpenTelemetry");
var serviceName = otelConfig["ServiceName"] ?? "Projections";
var otelEndpoint = otelConfig["Endpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddSource("MassTransit")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otelEndpoint);
        }));

// ===== Health Checks =====
builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        rabbitConnectionString: $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}:5672",
        name: "rabbitmq")
    .AddRedis(redisConnectionString, name: "redis");

var app = builder.Build();

// ===== Endpoints =====
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { service = "Projections", status = "running" }));

var dimensions = new[] { "status", "currency", "customer-ref", "shipping-method", "created-month", "created-year", "delivered-month", "delivered-year", "product" };

app.MapGet("/api/projections/stats", async (IRedisProjectionService projection, CancellationToken ct) =>
{
    var total = await projection.GetOrderCountAsync(ct);
    var lastUpdated = await projection.GetLastUpdatedAsync(ct);

    var result = new Dictionary<string, object?>
    {
        ["totalOrders"] = total,
        ["lastUpdated"] = lastUpdated
    };

    foreach (var dim in dimensions)
    {
        var data = await projection.GetAllByDimensionAsync(dim, ct);
        result[dim] = data;
    }

    return Results.Ok(result);
});

app.MapGet("/api/projections/stats/{dimension}", async (string dimension, IRedisProjectionService projection, CancellationToken ct) =>
{
    if (!dimensions.Contains(dimension))
        return Results.NotFound(new { error = $"Unknown dimension: {dimension}", available = dimensions });

    var data = await projection.GetAllByDimensionAsync(dimension, ct);
    return Results.Ok(data);
});

app.MapPost("/api/projections/flush", async (IRedisProjectionService projection, CancellationToken ct) =>
{
    await projection.FlushProjectionsAsync(ct);
    return Results.Ok(new { message = "Projections flushed" });
});

// ===== Startup Logging =====
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Projections");
logger.LogInformation("Projections service starting on port {Port}...",
    builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5030");
logger.LogInformation("Redis: {Redis}", redisConnectionString);
logger.LogInformation("OrderingApi: {BaseUrl}", builder.Configuration["OrderingApi:BaseUrl"] ?? "http://localhost:5001");
logger.LogInformation("RabbitMQ: {Host}", builder.Configuration["RabbitMQ:Host"]);
logger.LogInformation("OpenTelemetry: {ServiceName} -> {Endpoint}", serviceName, otelEndpoint);
logger.LogInformation("Projection dimensions: {Dimensions}", string.Join(", ", dimensions));

app.Run();
