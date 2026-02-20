using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var authConfig = builder.Configuration.GetSection("Authentication");
var authority = authConfig["Authority"];
var audience = authConfig["Audience"];
var requireHttpsMetadata = authConfig.GetValue<bool>("RequireHttpsMetadata");

// JWT Bearer validation (Keycloak as issuer)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = requireHttpsMetadata;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

// Add YARP reverse proxy from config (ReverseProxy section in appsettings)
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Health and info at root (no proxy, no auth required)
app.MapGet("/", () => Results.Ok(new { Service = "Gateway", Status = "Running", Description = "YARP reverse proxy for Distributed Playground APIs" }));
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

// Forward all other requests via YARP; require valid JWT
app.MapReverseProxy().RequireAuthorization();

app.Run();
