var builder = WebApplication.CreateBuilder(args);

// Add YARP reverse proxy from config (ReverseProxy section in appsettings)
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Health and info at root (no proxy)
app.MapGet("/", () => Results.Ok(new { Service = "Gateway", Status = "Running", Description = "YARP reverse proxy for Distributed Playground APIs" }));
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

// Forward all other requests via YARP (routes defined in appsettings)
app.MapReverseProxy();

app.Run();
