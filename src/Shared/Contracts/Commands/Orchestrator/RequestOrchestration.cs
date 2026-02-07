namespace Contracts.Commands.Orchestrator;

/// <summary>
/// Command to trigger a Semantic Kernel orchestration. The orchestrator runs the LLM with available plugins.
/// </summary>
public record RequestOrchestration
{
    /// <summary>
    /// User prompt or intent that drives the orchestration (e.g. "List recent orders and suggest next actions").
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Optional correlation id for tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
}
