using System.Diagnostics;
using AI.Processor.Services;
using Microsoft.AspNetCore.Mvc;

namespace AI.Processor.Endpoints;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai")
            .WithTags("AI");

        // ===== Chat/Completion Endpoints =====
        
        group.MapPost("/chat", async (
            [FromBody] ChatRequest request,
            [FromServices] IOllamaService ollamaService,
            CancellationToken cancellationToken) =>
        {
            var sw = Stopwatch.StartNew();
            
            var prompt = string.IsNullOrEmpty(request.SystemPrompt)
                ? request.Prompt
                : $"{request.SystemPrompt}\n\n{request.Prompt}";
            
            var response = await ollamaService.GenerateCompletionAsync(prompt, cancellationToken);
            sw.Stop();
            
            return Results.Ok(new ChatResponse(
                Response: response,
                Model: "llama3.2",
                Duration: sw.Elapsed
            ));
        })
        .WithName("Chat")
        .WithSummary("Send a prompt to Ollama and get a completion")
        .WithDescription("Sends a text prompt to the Ollama LLM and returns the generated response. Optionally include a system prompt for context.")
        .Produces<ChatResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/analyze", async (
            [FromBody] AnalyzeTextRequest request,
            [FromServices] IOllamaService ollamaService,
            CancellationToken cancellationToken) =>
        {
            var sw = Stopwatch.StartNew();
            
            var analysisType = request.AnalysisType ?? "general";
            var response = await ollamaService.AnalyzeOrderEventAsync(analysisType, request.Text, cancellationToken);
            sw.Stop();
            
            return Results.Ok(new AnalyzeTextResponse(
                Analysis: response,
                AnalysisType: analysisType,
                Duration: sw.Elapsed
            ));
        })
        .WithName("AnalyzeText")
        .WithSummary("Analyze text with AI")
        .WithDescription("Sends text to the LLM for analysis. Specify an analysis type for targeted insights.")
        .Produces<AnalyzeTextResponse>(StatusCodes.Status200OK);

        group.MapPost("/summarize", async (
            [FromBody] SummarizeRequest request,
            [FromServices] IOllamaService ollamaService,
            CancellationToken cancellationToken) =>
        {
            var sw = Stopwatch.StartNew();
            
            var prompt = $"""
                Please summarize the following text concisely{(request.MaxLength.HasValue ? $" in no more than {request.MaxLength} words" : "")}:
                
                {request.Text}
                
                Summary:
                """;
            
            var response = await ollamaService.GenerateCompletionAsync(prompt, cancellationToken);
            sw.Stop();
            
            return Results.Ok(new SummarizeResponse(
                Summary: response.Trim(),
                Duration: sw.Elapsed
            ));
        })
        .WithName("SummarizeText")
        .WithSummary("Summarize text with AI")
        .WithDescription("Generates a concise summary of the provided text using the LLM.")
        .Produces<SummarizeResponse>(StatusCodes.Status200OK);

        // ===== Embedding Endpoints =====
        
        group.MapPost("/embed", async (
            [FromBody] EmbeddingRequest request,
            [FromServices] IOllamaService ollamaService,
            CancellationToken cancellationToken) =>
        {
            var embedding = await ollamaService.GenerateEmbeddingAsync(request.Text, cancellationToken);
            
            return Results.Ok(new EmbeddingResponse(
                Embedding: embedding,
                Dimensions: embedding.Length,
                Model: "nomic-embed-text"
            ));
        })
        .WithName("GenerateEmbedding")
        .WithSummary("Generate text embedding")
        .WithDescription("Generates a vector embedding for the provided text using nomic-embed-text model.")
        .Produces<EmbeddingResponse>(StatusCodes.Status200OK);

        group.MapPost("/embed/batch", async (
            [FromBody] BatchEmbeddingRequest request,
            [FromServices] IOllamaService ollamaService,
            CancellationToken cancellationToken) =>
        {
            var results = new List<EmbeddingResult>();
            
            for (int i = 0; i < request.Texts.Count; i++)
            {
                var embedding = await ollamaService.GenerateEmbeddingAsync(request.Texts[i], cancellationToken);
                results.Add(new EmbeddingResult(i, embedding));
            }
            
            return Results.Ok(new BatchEmbeddingResponse(
                Embeddings: results,
                Model: "nomic-embed-text"
            ));
        })
        .WithName("GenerateBatchEmbeddings")
        .WithSummary("Generate embeddings for multiple texts")
        .WithDescription("Generates vector embeddings for multiple texts in a single request.")
        .Produces<BatchEmbeddingResponse>(StatusCodes.Status200OK);

        // ===== Semantic Search Endpoints =====
        
        group.MapPost("/search", async (
            [FromBody] SemanticSearchRequest request,
            [FromServices] IOllamaService ollamaService,
            [FromServices] IQdrantService qdrantService,
            CancellationToken cancellationToken) =>
        {
            var sw = Stopwatch.StartNew();
            
            // Generate embedding for the query
            var queryEmbedding = await ollamaService.GenerateEmbeddingAsync(request.Query, cancellationToken);
            
            // Search in Qdrant
            var results = await qdrantService.SearchSimilarOrdersAsync(queryEmbedding, request.Limit, cancellationToken);
            sw.Stop();
            
            return Results.Ok(new SemanticSearchResponse(
                Results: results.Select(r => new SearchResultDto(r.OrderId, r.Score, r.Payload)).ToList(),
                Duration: sw.Elapsed
            ));
        })
        .WithName("SemanticSearch")
        .WithSummary("Semantic search over orders")
        .WithDescription("Performs semantic search over stored order embeddings in Qdrant. Returns orders similar to the query text.")
        .Produces<SemanticSearchResponse>(StatusCodes.Status200OK);

        // ===== Info Endpoints =====
        
        group.MapGet("/info", (IConfiguration configuration) =>
        {
            return Results.Ok(new ModelInfoResponse(
                ChatModel: configuration["Ollama:Model"] ?? "llama3.2",
                EmbeddingModel: configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text",
                OllamaEndpoint: configuration["Ollama:BaseUrl"] ?? "http://localhost:11434",
                QdrantEndpoint: $"{configuration["Qdrant:Host"] ?? "localhost"}:{configuration["Qdrant:Port"] ?? "6334"}",
                QdrantCollection: configuration["Qdrant:CollectionName"] ?? "orders"
            ));
        })
        .WithName("GetAiInfo")
        .WithSummary("Get AI service configuration")
        .WithDescription("Returns information about the configured AI models and endpoints.")
        .Produces<ModelInfoResponse>(StatusCodes.Status200OK);

        group.MapGet("/health/ollama", async (
            [FromServices] IOllamaService ollamaService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                // Try a simple completion to check if Ollama is responding
                var response = await ollamaService.GenerateCompletionAsync("Say 'OK' if you're working.", cancellationToken);
                return Results.Ok(new { Status = "Healthy", Response = response.Substring(0, Math.Min(50, response.Length)) });
            }
            catch (Exception ex)
            {
                return Results.Json(new { Status = "Unhealthy", Error = ex.Message }, statusCode: 503);
            }
        })
        .WithName("CheckOllamaHealth")
        .WithSummary("Check Ollama connectivity")
        .WithDescription("Tests connectivity to the Ollama service by sending a simple prompt.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/health/qdrant", async (
            [FromServices] IQdrantService qdrantService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await qdrantService.EnsureCollectionExistsAsync(cancellationToken);
                return Results.Ok(new { Status = "Healthy", Collection = "orders" });
            }
            catch (Exception ex)
            {
                return Results.Json(new { Status = "Unhealthy", Error = ex.Message }, statusCode: 503);
            }
        })
        .WithName("CheckQdrantHealth")
        .WithSummary("Check Qdrant connectivity")
        .WithDescription("Tests connectivity to the Qdrant vector database.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status503ServiceUnavailable);
    }
}
