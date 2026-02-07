using Microsoft.SemanticKernel;

namespace Orchestrator.Api.Endpoints;

public static class OrchestratorEndpoints
{
    public static void MapOrchestratorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orchestrator").WithTags("Orchestrator");

        group.MapPost("/chat", async (ChatRequest request, Kernel kernel, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return Results.BadRequest(new { Message = "Prompt is required." });
            var result = await kernel.InvokePromptAsync(request.Prompt, cancellationToken: cancellationToken);
            var response = result.GetValue<string>() ?? string.Empty;
            return Results.Ok(new ChatResponse(response));
        })
        .WithName("Chat")
        .WithSummary("Chat with Semantic Kernel")
        .WithDescription("Sends the prompt to the LLM (Ollama) with access to plugins: ServicesApi (orders/customers HTTP) and MassTransitCommands (send commands).");

        group.MapPost("/prompt", async (PromptRequest request, Kernel kernel, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return Results.BadRequest(new { Message = "Prompt is required." });
            var result = await kernel.InvokePromptAsync(request.Prompt, cancellationToken: cancellationToken);
            var response = result.GetValue<string>() ?? string.Empty;
            return Results.Ok(new { Result = response });
        })
        .WithName("InvokePrompt")
        .WithSummary("Invoke a prompt (no chat history)");
    }
}

public record ChatRequest(string Prompt);
public record ChatResponse(string Response);
public record PromptRequest(string Prompt);
