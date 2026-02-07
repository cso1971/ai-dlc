using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace Orchestrator.Api.Endpoints;

public static class OrchestratorEndpoints
{
    private const string SystemPrompt = """
        Sei un assistente che può eseguire azioni sul sistema. Hai a disposizione questi plugin:
        - ServicesApi: per leggere ordini (GetOrders, GetOrderStats, GetOrderById) e clienti (GetCustomers).
        - MassTransitCommands: per creare un cliente (SendCreateCustomer: companyName, displayName, email, phone) o inviare un ordine (SendCreateOrder).
        Quando l'utente chiede di "creare un cliente" o "registrare un cliente", DEVI chiamare subito MassTransitCommands.SendCreateCustomer con i dati forniti (nome, email, ecc.). Non suggerire file JSON o altri metodi: esegui direttamente la funzione.
        Quando l'utente chiede di creare un ordine, usa MassTransitCommands.SendCreateOrder con customerId (GUID esistente), productCode, description, quantity, unitPrice.
        Rispondi in italiano, in modo conciso.
        """;

    public static void MapOrchestratorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orchestrator").WithTags("Orchestrator");

        group.MapPost("/chat", async (ChatRequest request, Kernel kernel, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            var prompt = request?.Prompt ?? "";
            if (string.IsNullOrWhiteSpace(prompt))
                return Results.Ok(new ChatResponse("Prompt vuoto. Scrivi una domanda o una richiesta."));
            try
            {
                var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
                var args = new KernelArguments(settings);
                var fullPrompt = $"<system>\n{SystemPrompt}\n</system>\n<user>\n{prompt}\n</user>";
                var result = await kernel.InvokePromptAsync(fullPrompt, args, null, null, cancellationToken);
                var response = result?.GetValue<string>() ?? string.Empty;
                return Results.Ok(new ChatResponse(response));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Orchestrator chat failed for prompt: {Prompt}", prompt);
                var message = ex.Message;
                if (ex.InnerException != null)
                    message += " " + ex.InnerException.Message;
                return Results.Ok(new ChatResponse($"Si è verificato un errore durante l'elaborazione: {message}."));
            }
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

public record ChatRequest([property: JsonPropertyName("prompt")] string Prompt);
public record ChatResponse([property: JsonPropertyName("response")] string Response);
public record PromptRequest(string Prompt);
