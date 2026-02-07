using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Customers.Api.Endpoints;
using Customers.Api.Infrastructure;
using Customers.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ===========================================
// Configuration
// ===========================================
var configuration = builder.Configuration;
var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "Customers.Api";

// ===========================================
// Database - Entity Framework Core
// ===========================================
builder.Services.AddDbContext<CustomersDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("PostgreSQL")));

// ===========================================
// Application Services
// ===========================================
builder.Services.AddScoped<ICustomersRepository, CustomersRepository>();
builder.Services.AddScoped<CustomerService>();

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
        Title = "Customers API",
        Version = "v1",
        Description = "Customer management API for the Distributed Playground",
        Contact = new OpenApiContact
        {
            Name = "Distributed Playground"
        }
    });
});

// ===========================================
// CORS (for Angular frontend)
// ===========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ===========================================
// Middleware Pipeline
// ===========================================
app.UseCors("AllowFrontend");

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Customers API v1");
    options.RoutePrefix = "swagger";
});

// Skip HTTPS redirect in Development so frontend (http://localhost:4200) can call http://localhost:5003
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Health check endpoint
app.MapHealthChecks("/health");

// Service info endpoint
app.MapGet("/", () => Results.Ok(new { Service = serviceName, Status = "Running" }))
    .WithTags("Health")
    .WithSummary("Service status")
    .ExcludeFromDescription();

// Customer REST API
app.MapCustomerEndpoints();

app.Run();
