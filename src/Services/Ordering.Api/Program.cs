using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Ordering.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ===========================================
// Configuration
// ===========================================
var configuration = builder.Configuration;
var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "Ordering.Api";

// ===========================================
// Database - Entity Framework Core
// ===========================================
builder.Services.AddDbContext<OrderingDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("PostgreSQL")));

// ===========================================
// MassTransit + RabbitMQ
// ===========================================
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    
    // Register consumers from this assembly
    x.AddConsumers(typeof(Program).Assembly);
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// ===========================================
// OpenTelemetry
// ===========================================
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(MassTransit.Logging.DiagnosticHeaders.DefaultListenerName)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
            });
    });

// ===========================================
// Health Checks
// ===========================================
builder.Services.AddHealthChecks()
    .AddNpgSql(configuration.GetConnectionString("PostgreSQL")!, name: "postgresql")
    .AddRabbitMQ(rabbitConnectionString: $"amqp://{configuration["RabbitMQ:Username"]}:{configuration["RabbitMQ:Password"]}@{configuration["RabbitMQ:Host"]}:5672", name: "rabbitmq");

// ===========================================
// API
// ===========================================
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ===========================================
// Middleware Pipeline
// ===========================================
app.UseHttpsRedirection();

// Health check endpoint
app.MapHealthChecks("/health");

// Sample endpoint - replace with your domain endpoints
app.MapGet("/", () => Results.Ok(new { Service = serviceName, Status = "Running" }));

app.MapGet("/api/orders", () =>
{
    return Results.Ok(new[] { new { Id = 1, Description = "Sample Order" } });
});

app.Run();
