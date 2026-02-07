using AI.Processor.Clients;
using AI.Processor.Services;
using Contracts.Events.Customers;
using MassTransit;

namespace AI.Processor.Consumers;

public class CustomerCreatedConsumer : IConsumer<CustomerCreated>
{
    private readonly IOllamaService _ollamaService;
    private readonly IQdrantService _qdrantService;
    private readonly ICustomerApiClient _customerApiClient;
    private readonly ILogger<CustomerCreatedConsumer> _logger;

    public CustomerCreatedConsumer(
        IOllamaService ollamaService,
        IQdrantService qdrantService,
        ICustomerApiClient customerApiClient,
        ILogger<CustomerCreatedConsumer> logger)
    {
        _ollamaService = ollamaService;
        _qdrantService = qdrantService;
        _customerApiClient = customerApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CustomerCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing CustomerCreated event for Customer {CustomerId}", message.CustomerId);

        try
        {
            var customer = await _customerApiClient.GetCustomerAsync(message.CustomerId, context.CancellationToken);

            if (customer == null)
            {
                _logger.LogWarning("Could not fetch customer {CustomerId} from API, skipping RAG indexing", message.CustomerId);
                return;
            }

            var customerText = customer.ToTextForEmbedding();
            var embedding = await _ollamaService.GenerateEmbeddingAsync(customerText, context.CancellationToken);

            var payload = BuildCustomerPayload(customer);

            await _qdrantService.UpsertCustomerAsync(message.CustomerId, embedding, payload, context.CancellationToken);

            var analysisPrompt = $"""
                Analyze this new customer and provide business insights:

                {customerText}

                Consider: segment potential, geographic reach, billing/shipping setup, preferred language/currency.
                """;

            var analysis = await _ollamaService.GenerateCompletionAsync(analysisPrompt, context.CancellationToken);

            _logger.LogInformation("Customer {CustomerId} processed and stored in RAG. Company: {CompanyName}. Analysis: {Analysis}",
                message.CustomerId, customer.CompanyName,
                analysis.Substring(0, Math.Min(200, analysis.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing CustomerCreated event for Customer {CustomerId}", message.CustomerId);
            throw;
        }
    }

    private static Dictionary<string, object> BuildCustomerPayload(CustomerResponse customer)
    {
        return new Dictionary<string, object>
        {
            ["customerId"] = customer.Id.ToString(),
            ["companyName"] = customer.CompanyName,
            ["displayName"] = customer.DisplayName ?? "",
            ["email"] = customer.Email,
            ["phone"] = customer.Phone ?? "",
            ["taxId"] = customer.TaxId ?? "",
            ["vatNumber"] = customer.VatNumber ?? "",
            ["preferredLanguage"] = customer.PreferredLanguage,
            ["preferredCurrency"] = customer.PreferredCurrency,
            ["createdAt"] = customer.CreatedAt.ToString("O"),
            ["billingCity"] = customer.BillingAddress?.City ?? "",
            ["billingCountry"] = customer.BillingAddress?.CountryCode ?? "",
            ["shippingCity"] = customer.ShippingAddress?.City ?? "",
            ["shippingCountry"] = customer.ShippingAddress?.CountryCode ?? "",
            ["customerText"] = customer.ToTextForEmbedding()
        };
    }
}
