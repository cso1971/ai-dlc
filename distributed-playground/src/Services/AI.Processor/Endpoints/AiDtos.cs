namespace AI.Processor.Endpoints;

// ===== Chat/Completion DTOs =====

public record ChatRequest(
    string Prompt,
    string? SystemPrompt = null,
    float? Temperature = null,
    int? MaxTokens = null,
    int? MaxResults = null
);

public record ChatResponse(
    string Response,
    string Model,
    TimeSpan Duration,
    int? ContextOrdersCount = null,
    int? ContextCustomersCount = null
);

public record StreamChatRequest(
    string Prompt,
    string? SystemPrompt = null
);

// ===== Embedding DTOs =====

public record EmbeddingRequest(
    string Text
);

public record EmbeddingResponse(
    float[] Embedding,
    int Dimensions,
    string Model
);

public record BatchEmbeddingRequest(
    IReadOnlyList<string> Texts
);

public record BatchEmbeddingResponse(
    IReadOnlyList<EmbeddingResult> Embeddings,
    string Model
);

public record EmbeddingResult(
    int Index,
    float[] Embedding
);

// ===== Semantic Search DTOs =====

public record SemanticSearchRequest(
    string Query,
    int Limit = 10
);

public record SemanticSearchResponse(
    IReadOnlyList<SearchResultDto> Results,
    TimeSpan Duration
);

public record SearchResultDto(
    Guid OrderId,
    float Score,
    Dictionary<string, object> Metadata
);

// ===== Analysis DTOs =====

public record AnalyzeTextRequest(
    string Text,
    string? AnalysisType = null
);

public record AnalyzeTextResponse(
    string Analysis,
    string AnalysisType,
    TimeSpan Duration
);

public record SummarizeRequest(
    string Text,
    int? MaxLength = null
);

public record SummarizeResponse(
    string Summary,
    TimeSpan Duration
);

// ===== Model Info DTOs =====

public record ModelInfoResponse(
    string ChatModel,
    string EmbeddingModel,
    string OllamaEndpoint,
    string QdrantEndpoint,
    string QdrantCollection
);
