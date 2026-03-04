using AI.Processor.Services;
using Contracts.Events.Customers;
using MassTransit;

namespace AI.Processor.Consumers;

public class CustomerCancelledConsumer : IConsumer<CustomerCancelled>
{
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<CustomerCancelledConsumer> _logger;

    public CustomerCancelledConsumer(IQdrantService qdrantService, ILogger<CustomerCancelledConsumer> logger)
    {
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CustomerCancelled> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing CustomerCancelled event for Customer {CustomerId}", message.CustomerId);

        try
        {
            await _qdrantService.DeleteCustomerAsync(message.CustomerId, context.CancellationToken);
            _logger.LogInformation("Customer {CustomerId} removed from RAG (cancelled)", message.CustomerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing CustomerCancelled event for Customer {CustomerId}", message.CustomerId);
            throw;
        }
    }
}
