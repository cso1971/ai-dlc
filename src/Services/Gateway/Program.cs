using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "Gateway";

var authConfig = configuration.GetSection("Authentication");
var authority = authConfig["Authority"];
var requireHttpsMetadata = authConfig.GetValue<bool>("RequireHttpsMetadata");

// CORS so Angular (localhost:4200) can call the Gateway
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// JWT Bearer validation (Keycloak as issuer). Accept playground-api (backend) and ordering-web (SPA) tokens.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.RequireHttpsMetadata = requireHttpsMetadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudiences = new[] { "playground-api", "ordering-web", "account" },
            ValidateIssuer = true,
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());
});

// Add YARP reverse proxy from config (ReverseProxy section in appsettings)
builder.Services.AddReverseProxy()
    .LoadFromConfig(configuration.GetSection("ReverseProxy"));

// OpenTelemetry distributed tracing → Jaeger
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
            });
    });

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI aggregating all backend services
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/ordering/swagger/v1/swagger.json", "Ordering API");
    options.SwaggerEndpoint("/customers/swagger/v1/swagger.json", "Customers API");
    options.SwaggerEndpoint("/ai/swagger/v1/swagger.json", "AI Processor API");
    options.SwaggerEndpoint("/orchestrator/swagger/v1/swagger.json", "Orchestrator API");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Distributed Playground - API Gateway";
});

// Health and info at root (no proxy, no auth required)
app.MapGet("/", () => Results.Ok(new { Service = "Gateway", Status = "Running", Description = "YARP reverse proxy for Distributed Playground APIs" }));
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

// YARP: swagger routes are public, API routes require JWT
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        var route = context.GetReverseProxyFeature().Route;
        var isPublic = route.Config.Metadata?.ContainsKey("Public") == true;

        if (!isPublic)
        {
            var authResult = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!authResult.Succeeded)
            {
                context.Response.StatusCode = 401;
                return;
            }
            context.User = authResult.Principal!;
        }

        await next();
    });
});

app.Run();
