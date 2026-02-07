using MassTransit;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Orchestrator.Api.Clients;
using Orchestrator.Api.Consumers;
using Orchestrator.Api.Endpoints;
using Orchestrator.Api.Plugins;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// ===== HTTP clients for plugins =====
var orderingBase = builder.Configuration["OrderingApi:BaseUrl"] ?? "http://localhost:5001";
var customersBase = builder.Configuration["CustomersApi:BaseUrl"] ?? "http://localhost:5003";

builder.Services.AddHttpClient<IOrderingApiClient, OrderingApiClient>(client =>
{
    client.BaseAddress = new Uri(orderingBase.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry))));

builder.Services.AddHttpClient<ICustomersApiClient, CustomersApiClient>(client =>
{
    client.BaseAddress = new Uri(customersBase.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry))));

// ===== Plugin types (resolved from DI when kernel uses them) =====
builder.Services.AddTransient<ServicesApiPlugin>();
builder.Services.AddTransient<MassTransitCommandsPlugin>();

// ===== Semantic Kernel with Ollama (local only) =====
builder.Services.AddSingleton<Kernel>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var ollamaEndpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
    var ollamaModel = config["Ollama:ModelId"] ?? "llama3.2";
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOllamaChatCompletion(modelId: ollamaModel, endpoint: new Uri(ollamaEndpoint));
    kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<ServicesApiPlugin>(), "ServicesApi");
    kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<MassTransitCommandsPlugin>(), "MassTransitCommands");
    return kernelBuilder.Build();
});

// ===== MassTransit =====
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RequestOrchestrationConsumer>();
    x.SetKebabCaseEndpointNameFormatter();
    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var user = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var pass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
        cfg.Host(host, "/", h => { h.Username(user); h.Password(pass); });
        cfg.ConfigureEndpoints(context);
    });
});

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Orchestrator API (Semantic Kernel)", Version = "v1" });
});

// ===== Health =====
builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}:5672",
        name: "rabbitmq");

// ===== CORS =====
builder.Services.AddCors(p => p.AddPolicy("AllowAll", x => x.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger().UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Orchestrator API v1"); c.RoutePrefix = "swagger"; });
app.UseCors("AllowAll");
app.MapOrchestratorEndpoints();
app.MapHealthChecks("/health");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Orchestrator.Api (Semantic Kernel) starting on http://localhost:5020");
logger.LogInformation("Ollama: {Endpoint}, Model: {Model}", builder.Configuration["Ollama:Endpoint"], builder.Configuration["Ollama:ModelId"]);

app.Run();
