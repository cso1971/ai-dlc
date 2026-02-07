using System.Text.Json;
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

                // Fallback: Ollama può restituire la "chiamata" come JSON nel testo invece che come tool_call; invochiamo noi il plugin
                logger.LogDebug("Chat response length={Len}, startsWith={{={Starts}", response.Length, response.TrimStart().StartsWith('{'));
                var invoked = await TryInvokeFunctionFromResponseAsync(kernel, response.Trim(), logger, cancellationToken);
                if (invoked != null)
                    return Results.Ok(new ChatResponse(invoked));

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

    /// <summary>
    /// Se la risposta del modello è un JSON tipo {"name":"Plugin-Function","parameters":{...}},
    /// invoca la funzione e restituisce il risultato (Ollama a volte restituisce la "call" come testo invece che come tool_call).
    /// </summary>
    private static async Task<string?> TryInvokeFunctionFromResponseAsync(Kernel kernel, string response, ILogger logger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(response) || !response.Contains("parameters", StringComparison.Ordinal)) return null;

        // Estrai il primo oggetto JSON (da prima { a } bilanciata), così funziona anche senza markdown
        var json = ExtractJsonObject(response);
        if (string.IsNullOrEmpty(json))
        {
            logger.LogDebug("Fallback: no JSON object in response (len={Len})", response?.Length ?? 0);
            return null;
        }

        JsonElement root;
        try { root = JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return null; }

        if (!root.TryGetProperty("name", out var nameEl) || !root.TryGetProperty("parameters", out var paramsEl) || paramsEl.ValueKind != JsonValueKind.Object)
            return null;

        var name = nameEl.GetString();
        if (string.IsNullOrEmpty(name) || !name.Contains('-')) return null;

        var parts = name.Split('-', 2);
        var pluginName = parts[0];
        var functionName = parts[1];

        var kernelArgs = new KernelArguments();
        foreach (var prop in paramsEl.EnumerateObject())
        {
            var v = prop.Value;
            if (v.ValueKind == JsonValueKind.String) kernelArgs[prop.Name] = v.GetString();
            else if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) kernelArgs[prop.Name] = i;
            else if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) kernelArgs[prop.Name] = d;
            else kernelArgs[prop.Name] = v.GetRawText();
        }

        try
        {
            var function = kernel.Plugins.GetFunction(pluginName, functionName);
            var fnResult = await kernel.InvokeAsync(function, kernelArgs, cancellationToken);
            var resultValue = fnResult?.GetValue<string>();
            logger.LogInformation("Fallback: invoked {Plugin}.{Function}", pluginName, functionName);
            return resultValue ?? "Comando eseguito.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallback: invoke failed for {Plugin}.{Function} - {Message}", pluginName, functionName, ex.Message);
            return null;
        }
    }

    /// <summary>Estrae il primo oggetto JSON dalla risposta (da prima { a } bilanciata).</summary>
    private static string? ExtractJsonObject(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        var s = response.Trim();
        var start = s.IndexOf('{');
        if (start < 0) return null;
        var depth = 0;
        for (var i = start; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}')
            {
                depth--;
                if (depth == 0) return s[start..(i + 1)];
            }
        }
        return null;
    }
}

public record ChatRequest([property: JsonPropertyName("prompt")] string Prompt);
public record ChatResponse([property: JsonPropertyName("response")] string Response);
public record PromptRequest(string Prompt);
