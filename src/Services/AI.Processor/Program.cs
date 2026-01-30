using AI.Processor.Consumers;
using AI.Processor.Services;
using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// ===== Services =====
builder.Services.AddSingleton<IOllamaService, OllamaService>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();

// ===== MassTransit with RabbitMQ =====
builder.Services.AddMassTransit(x =>
{
    // Register all consumers
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<OrderStatusChangedConsumer>();
    x.AddConsumer<OrderShippedConsumer>();
    x.AddConsumer<OrderDeliveredConsumer>();
    x.AddConsumer<OrderCancelledConsumer>();
    x.AddConsumer<OrderCompletedConsumer>();

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
var serviceName = otelConfig["ServiceName"] ?? "AI.Processor";
var otelEndpoint = otelConfig["Endpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddSource("MassTransit")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otelEndpoint);
        }));

// ===== Health Checks =====
builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        rabbitConnectionString: $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}:5672",
        name: "rabbitmq");

var host = builder.Build();

// Log startup information
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AI.Processor");
logger.LogInformation("AI.Processor starting...");
logger.LogInformation("OpenTelemetry configured with service name: {ServiceName}", serviceName);
logger.LogInformation("OpenTelemetry exporter endpoint: {Endpoint}", otelEndpoint);
logger.LogInformation("RabbitMQ host: {Host}", builder.Configuration["RabbitMQ:Host"]);
logger.LogInformation("Ollama endpoint: {Endpoint}", builder.Configuration["Ollama:BaseUrl"]);
logger.LogInformation("Qdrant endpoint: {Host}:{Port}", 
    builder.Configuration["Qdrant:Host"], 
    builder.Configuration["Qdrant:Port"]);

host.Run();
