using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Ordering.Api.Endpoints;
using Ordering.Api.Infrastructure;
using Ordering.Api.Services;

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
// Application Services
// ===========================================
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<OrderingService>();

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
// OpenAPI / Swagger
// ===========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ordering API",
        Version = "v1",
        Description = "Order management API for the Distributed Playground",
        Contact = new OpenApiContact
        {
            Name = "Distributed Playground"
        }
    });
});

var app = builder.Build();

// ===========================================
// Middleware Pipeline
// ===========================================

// Swagger UI (available in all environments for playground)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Ordering API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// Health check endpoint
app.MapHealthChecks("/health");

// Service info endpoint
app.MapGet("/", () => Results.Ok(new { Service = serviceName, Status = "Running" }))
    .WithTags("Health")
    .WithSummary("Service status")
    .ExcludeFromDescription();

// Order REST API
app.MapOrderEndpoints();

app.Run();
