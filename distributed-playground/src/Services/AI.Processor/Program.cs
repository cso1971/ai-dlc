using AI.Processor.Clients;
using AI.Processor.Consumers;
using AI.Processor.Endpoints;
using AI.Processor.Services;
using MassTransit;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// ===== Services =====
builder.Services.AddSingleton<IOllamaService, OllamaService>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();

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

// ===== Customers API Client with Polly =====
builder.Services.AddHttpClient<ICustomerApiClient, CustomerApiClient>(client =>
{
    var baseUrl = builder.Configuration["CustomersApi:BaseUrl"] ?? "http://localhost:5003";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// ===== Projections API Client with Polly =====
builder.Services.AddHttpClient<IProjectionApiClient, ProjectionApiClient>(client =>
{
    var baseUrl = builder.Configuration["ProjectionsApi:BaseUrl"] ?? "http://localhost:5030";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// ===== Swagger/OpenAPI =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Processor API",
        Version = "v1",
        Description = "REST API for AI-powered text processing using Ollama LLM and Qdrant vector database",
        Contact = new OpenApiContact
        {
            Name = "Distributed Playground"
        }
    });
});

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
    x.AddConsumer<CustomerCreatedConsumer>();
    x.AddConsumer<CustomerUpdatedConsumer>();
    x.AddConsumer<CustomerCancelledConsumer>();

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
        name: "rabbitmq");

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ===== Middleware Pipeline =====

// Swagger UI (available in all environments for this playground)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Processor API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

// ===== Map Endpoints =====
app.MapAiEndpoints();
app.MapHealthChecks("/health");

// ===== Startup Logging =====
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AI.Processor");
logger.LogInformation("AI.Processor starting...");
logger.LogInformation("Swagger UI available at: /swagger");
logger.LogInformation("OpenTelemetry configured with service name: {ServiceName}", serviceName);
logger.LogInformation("OpenTelemetry exporter endpoint: {Endpoint}", otelEndpoint);
logger.LogInformation("RabbitMQ host: {Host}", builder.Configuration["RabbitMQ:Host"]);
logger.LogInformation("Ollama endpoint: {Endpoint}", builder.Configuration["Ollama:BaseUrl"]);
logger.LogInformation("Qdrant endpoint: {Host}:{Port}", 
    builder.Configuration["Qdrant:Host"], 
    builder.Configuration["Qdrant:Port"]);

app.Run();
