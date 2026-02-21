using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Projections.Consumers;
using Projections.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ===== Redis =====
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IRedisProjectionService, RedisProjectionService>();

// ===== MassTransit with RabbitMQ =====
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedProjectionConsumer>();

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

app.MapGet("/api/projections/stats", async (IRedisProjectionService projection, CancellationToken ct) =>
{
    var total = await projection.GetOrderCountAsync(ct);
    var byStatus = await projection.GetAllOrdersByStatusAsync(ct);
    var byCurrency = await projection.GetAllOrdersByCurrencyAsync(ct);
    var lastUpdated = await projection.GetLastUpdatedAsync(ct);

    return Results.Ok(new
    {
        totalOrders = total,
        byStatus,
        byCurrency = byCurrency.ToDictionary(
            kvp => kvp.Key,
            kvp => new { count = kvp.Value.Count, totalValue = kvp.Value.TotalValue }),
        lastUpdated
    });
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
logger.LogInformation("RabbitMQ: {Host}", builder.Configuration["RabbitMQ:Host"]);
logger.LogInformation("OpenTelemetry: {ServiceName} -> {Endpoint}", serviceName, otelEndpoint);

app.Run();
