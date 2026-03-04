using Contracts.Commands.Orchestrator;
using MassTransit;
using Microsoft.SemanticKernel;

namespace Orchestrator.Api.Consumers;

/// <summary>
/// MassTransit consumer that triggers a Semantic Kernel orchestration when a RequestOrchestration command is received.
/// </summary>
public class RequestOrchestrationConsumer : IConsumer<RequestOrchestration>
{
    private readonly Kernel _kernel;
    private readonly ILogger<RequestOrchestrationConsumer> _logger;

    public RequestOrchestrationConsumer(Kernel kernel, ILogger<RequestOrchestrationConsumer> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RequestOrchestration> context)
    {
        var prompt = context.Message.Prompt;
        _logger.LogInformation("Orchestration requested: {Prompt}", prompt);

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: context.CancellationToken);
            var response = result.GetValue<string>() ?? "(no response)";
            _logger.LogInformation("Orchestration completed. Response length: {Length}", response.Length);
            // Optionally respond or publish result; for now we just log.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration failed for prompt: {Prompt}", prompt);
            throw;
        }
    }
}
