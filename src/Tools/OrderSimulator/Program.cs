using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using Bogus;
using Contracts.Commands.Ordering;
using Contracts.ValueObjects.Ordering;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ===== Faker Setup =====
var productFaker = new Faker<OrderLineItem>()
    .RuleFor(x => x.ProductCode, f => f.Commerce.Ean8())
    .RuleFor(x => x.Description, f => f.Commerce.ProductName())
    .RuleFor(x => x.Quantity, f => f.Random.Int(1, 10))
    .RuleFor(x => x.UnitOfMeasure, f => f.PickRandom("PCS", "KG", "M", "L", "BOX"))
    .RuleFor(x => x.UnitPrice, f => Math.Round(f.Random.Decimal(10, 500), 2))
    .RuleFor(x => x.DiscountPercent, f => f.Random.Bool(0.3f) ? f.Random.Int(5, 20) : 0)
    .RuleFor(x => x.TaxPercent, f => f.PickRandom(0m, 10m, 22m));

var addressFaker = new Faker<ShippingAddress>()
    .RuleFor(x => x.RecipientName, f => f.Name.FullName())
    .RuleFor(x => x.AddressLine1, f => f.Address.StreetAddress())
    .RuleFor(x => x.AddressLine2, f => f.Random.Bool(0.3f) ? f.Address.SecondaryAddress() : null)
    .RuleFor(x => x.City, f => f.Address.City())
    .RuleFor(x => x.StateOrProvince, f => f.Address.State())
    .RuleFor(x => x.PostalCode, f => f.Address.ZipCode())
    .RuleFor(x => x.CountryCode, f => f.PickRandom("IT", "DE", "FR", "ES", "US", "UK", "NL", "BE"))
    .RuleFor(x => x.PhoneNumber, f => f.Phone.PhoneNumber())
    .RuleFor(x => x.Notes, f => f.Random.Bool(0.2f) ? f.Lorem.Sentence() : null);

var orderFaker = new Faker<CreateOrder>()
    .RuleFor(x => x.CustomerId, f => f.Random.Guid())
    .RuleFor(x => x.CustomerReference, f => $"PO-{f.Random.AlphaNumeric(8).ToUpper()}")
    .RuleFor(x => x.RequestedDeliveryDate, f => DateOnly.FromDateTime(f.Date.Future(30)))
    .RuleFor(x => x.Priority, f => f.Random.Int(1, 5))
    .RuleFor(x => x.CurrencyCode, f => f.PickRandom("EUR", "USD", "GBP"))
    .RuleFor(x => x.PaymentTerms, f => f.PickRandom("NET30", "NET60", "COD", "Prepaid"))
    .RuleFor(x => x.ShippingMethod, f => f.PickRandom("Standard", "Express", "NextDay", "Economy"))
    .RuleFor(x => x.ShippingAddress, f => addressFaker.Generate())
    .RuleFor(x => x.Notes, f => f.Random.Bool(0.3f) ? f.Lorem.Sentence() : null)
    .RuleFor(x => x.Lines, f => productFaker.Generate(f.Random.Int(1, 5)));

// ===== CLI Options =====
var ordersOption = new Option<int>(
    name: "--orders",
    description: "Number of orders to create",
    getDefaultValue: () => 10);
ordersOption.AddAlias("-n");

var simulateWorkflowOption = new Option<bool>(
    name: "--simulate-workflow",
    description: "Simulate random workflow transitions after creating orders",
    getDefaultValue: () => true);
simulateWorkflowOption.AddAlias("-w");

var delayOption = new Option<int>(
    name: "--delay",
    description: "Delay in milliseconds between commands",
    getDefaultValue: () => 500);
delayOption.AddAlias("-d");

var rabbitHostOption = new Option<string>(
    name: "--rabbit-host",
    description: "RabbitMQ host",
    getDefaultValue: () => "localhost");

var rabbitUserOption = new Option<string>(
    name: "--rabbit-user",
    description: "RabbitMQ username",
    getDefaultValue: () => "playground");

var rabbitPasswordOption = new Option<string>(
    name: "--rabbit-password",
    description: "RabbitMQ password",
    getDefaultValue: () => "playground_pwd");

var customersApiOption = new Option<string>(
    name: "--customers-api",
    description: "Customers API base URL (to create/fetch customers)",
    getDefaultValue: () => "http://localhost:5003");

var customersOption = new Option<int>(
    name: "--customers",
    description: "Number of customers to create before orders (if none exist)",
    getDefaultValue: () => 10);
customersOption.AddAlias("-c");

var rootCommand = new RootCommand("Order Simulator - Generate test orders and simulate workflows")
{
    ordersOption,
    customersOption,
    simulateWorkflowOption,
    delayOption,
    rabbitHostOption,
    rabbitUserOption,
    rabbitPasswordOption,
    customersApiOption
};

rootCommand.SetHandler(async (int orders, int customersToCreate, bool simulateWorkflow, int delay, string rabbitHost, string rabbitUser, string rabbitPassword, string customersApi) =>
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║              ORDER SIMULATOR - Distributed Playground        ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"  👥 Customers to create (if needed): {customersToCreate}");
    Console.WriteLine($"  📦 Orders to create: {orders}");
    Console.WriteLine($"  🔄 Simulate workflow: {simulateWorkflow}");
    Console.WriteLine($"  ⏱️  Delay: {delay}ms");
    Console.WriteLine($"  🐰 RabbitMQ: {rabbitHost}");
    Console.WriteLine($"  👥 Customers API: {customersApi}");
    Console.WriteLine();

    // Customers API client
    var customersHttp = new HttpClient { BaseAddress = new Uri(customersApi.TrimEnd('/') + "/") };
    List<Guid> customerIds;
    try
    {
        var existing = await customersHttp.GetFromJsonAsync<List<CustomerSummaryDto>>("api/customers");
        customerIds = (existing ?? new List<CustomerSummaryDto>())
            .Where(c => c.IsActive)
            .Select(c => c.Id)
            .ToList();

        if (customerIds.Count == 0 && customersToCreate > 0)
        {
            // Phase 0: Create customers first
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"  PHASE 0: Creating {customersToCreate} Customers");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            var customerFaker = new Faker<CreateCustomerRequestDto>()
                .RuleFor(x => x.CompanyName, f => f.Company.CompanyName())
                .RuleFor(x => x.DisplayName, f => f.Company.CompanyName(1))
                .RuleFor(x => x.Email, f => f.Internet.Email())
                .RuleFor(x => x.Phone, f => f.Phone.PhoneNumber())
                .RuleFor(x => x.PreferredLanguage, f => f.PickRandom("en", "it", "de", "fr"))
                .RuleFor(x => x.PreferredCurrency, f => f.PickRandom("EUR", "USD", "GBP"));
            for (int i = 1; i <= customersToCreate; i++)
            {
                var req = customerFaker.Generate();
                var response = await customersHttp.PostAsJsonAsync("api/customers", req);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"⚠️  Failed to create customer {i}: {response.StatusCode}");
                    continue;
                }
                var created = await response.Content.ReadFromJsonAsync<CreateCustomerResponseDto>();
                if (created != null)
                {
                    customerIds.Add(created.Id);
                    Console.WriteLine($"  [{i:D2}/{customersToCreate}] 👤 {req.CompanyName} ({req.Email})");
                }
                await Task.Delay(delay);
            }
            Console.WriteLine($"✅ Created {customerIds.Count} customer(s)");
            Console.WriteLine();
        }
        else if (customerIds.Count == 0)
        {
            Console.WriteLine("⚠️  No active customers found. Use --customers 10 to create some, or create via frontend.");
            return;
        }
        else
        {
            Console.WriteLine($"✅ Using {customerIds.Count} existing customer(s)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Could not load/create customers from {customersApi}: {ex.Message}");
        Console.WriteLine("    Make sure Customers.Api is running (e.g. http://localhost:5003)");
        return;
    }

    var random = new Random();

    // Setup MassTransit
    var services = new ServiceCollection();
    services.AddMassTransit(x =>
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitHost, "/", h =>
            {
                h.Username(rabbitUser);
                h.Password(rabbitPassword);
            });
        });
    });

    var provider = services.BuildServiceProvider();
    var busControl = provider.GetRequiredService<IBusControl>();
    
    await busControl.StartAsync();
    Console.WriteLine("✅ Connected to RabbitMQ");
    Console.WriteLine();

    var createdOrderIds = new List<Guid>();
    var sendEndpoint = await busControl.GetSendEndpoint(new Uri("queue:create-order"));

    // ===== Phase 1: Create Orders =====
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  PHASE 1: Creating Orders");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    
    for (int i = 1; i <= orders; i++)
    {
        var order = orderFaker.Generate();
        var orderId = Guid.NewGuid();
        
        // Use reflection or create a new command with the ID
        var createCommand = new CreateOrder
        {
            CustomerId = customerIds[random.Next(customerIds.Count)],
            CustomerReference = order.CustomerReference,
            RequestedDeliveryDate = order.RequestedDeliveryDate,
            Priority = order.Priority,
            CurrencyCode = order.CurrencyCode,
            PaymentTerms = order.PaymentTerms,
            ShippingMethod = order.ShippingMethod,
            ShippingAddress = order.ShippingAddress,
            Notes = order.Notes,
            Lines = order.Lines
        };

        await sendEndpoint.Send(createCommand);
        createdOrderIds.Add(order.CustomerId); // We'll track by CustomerId for now
        
        var total = order.Lines?.Sum(l => l.Quantity * l.UnitPrice) ?? 0;
        Console.WriteLine($"  [{i:D3}/{orders}] 📦 Created order for {order.ShippingAddress?.City}, {order.ShippingAddress?.CountryCode} - {order.Lines?.Count ?? 0} items - {total:F2} {order.CurrencyCode}");
        
        await Task.Delay(delay);
    }

    Console.WriteLine();
    Console.WriteLine($"✅ Created {orders} orders");

    if (!simulateWorkflow)
    {
        Console.WriteLine();
        Console.WriteLine("Workflow simulation disabled. Exiting.");
        await busControl.StopAsync();
        return;
    }

    // Wait a bit for orders to be processed
    Console.WriteLine();
    Console.WriteLine("⏳ Waiting 3 seconds for orders to be created...");
    await Task.Delay(3000);

    // ===== Phase 2: Fetch created order IDs from API =====
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  PHASE 2: Fetching Created Orders from API");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");

    var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };
    List<OrderSummary> orderSummaries;
    
    try
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = await httpClient.GetFromJsonAsync<List<OrderSummary>>("api/orders", jsonOptions);
        orderSummaries = response?.Take(orders).ToList() ?? new List<OrderSummary>();
        Console.WriteLine($"✅ Fetched {orderSummaries.Count} orders from API");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Could not fetch orders from API: {ex.Message}");
        Console.WriteLine("    Make sure Ordering.Api is running on http://localhost:5001");
        await busControl.StopAsync();
        return;
    }

    if (orderSummaries.Count == 0)
    {
        Console.WriteLine("⚠️  No orders found. Exiting.");
        await busControl.StopAsync();
        return;
    }

    // ===== Phase 3: Simulate Workflow =====
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  PHASE 3: Simulating Workflow Transitions");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");

    var startProcessingEndpoint = await busControl.GetSendEndpoint(new Uri("queue:start-order-processing"));
    var shipEndpoint = await busControl.GetSendEndpoint(new Uri("queue:ship-order"));
    var deliverEndpoint = await busControl.GetSendEndpoint(new Uri("queue:deliver-order"));
    var invoiceEndpoint = await busControl.GetSendEndpoint(new Uri("queue:invoice-order"));
    var cancelEndpoint = await busControl.GetSendEndpoint(new Uri("queue:cancel-order"));

    var carriers = new[] { "DHL", "FedEx", "UPS", "GLS", "TNT", "Hermes" };
    var cancelReasons = new[] { 
        "Customer requested cancellation", 
        "Out of stock", 
        "Payment failed",
        "Address undeliverable",
        "Duplicate order"
    };

    int processed = 0;
    int shipped = 0;
    int delivered = 0;
    int invoiced = 0;
    int cancelled = 0;

    foreach (var orderSummary in orderSummaries)
    {
        var orderId = orderSummary.Id;
        
        // Decide what to do with this order
        var action = random.Next(100);
        
        if (action < 10) // 10% chance to cancel immediately
        {
            await cancelEndpoint.Send(new CancelOrder 
            { 
                OrderId = orderId, 
                CancellationReason = cancelReasons[random.Next(cancelReasons.Length)]
            });
            Console.WriteLine($"  ❌ Order {orderId.ToString()[..8]}... CANCELLED");
            cancelled++;
            await Task.Delay(delay);
            continue;
        }

        // Start processing
        await startProcessingEndpoint.Send(new StartOrderProcessing { OrderId = orderId });
        Console.WriteLine($"  🔄 Order {orderId.ToString()[..8]}... Started Processing");
        processed++;
        await Task.Delay(delay);

        if (action < 20) // 10% more chance to cancel after processing
        {
            await cancelEndpoint.Send(new CancelOrder 
            { 
                OrderId = orderId, 
                CancellationReason = cancelReasons[random.Next(cancelReasons.Length)]
            });
            Console.WriteLine($"  ❌ Order {orderId.ToString()[..8]}... CANCELLED (after processing)");
            cancelled++;
            await Task.Delay(delay);
            continue;
        }

        // Ship
        var carrier = carriers[random.Next(carriers.Length)];
        var trackingNumber = $"{carrier.ToUpper()[..3]}{random.Next(100000000, 999999999)}";
        await shipEndpoint.Send(new ShipOrder 
        { 
            OrderId = orderId,
            TrackingNumber = trackingNumber,
            Carrier = carrier,
            EstimatedDeliveryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(random.Next(1, 7)))
        });
        Console.WriteLine($"  🚚 Order {orderId.ToString()[..8]}... Shipped via {carrier}");
        shipped++;
        await Task.Delay(delay);

        if (action < 30) // Stop some orders at shipped
        {
            continue;
        }

        // Deliver
        var receivers = new[] { "John Smith", "Front Desk", "Neighbor", "Security", "Reception" };
        await deliverEndpoint.Send(new DeliverOrder 
        { 
            OrderId = orderId,
            ReceivedBy = receivers[random.Next(receivers.Length)],
            DeliveryNotes = random.Next(100) < 30 ? "Left at door" : null
        });
        Console.WriteLine($"  📬 Order {orderId.ToString()[..8]}... Delivered");
        delivered++;
        await Task.Delay(delay);

        if (action < 50) // Stop some orders at delivered
        {
            continue;
        }

        // Invoice
        await invoiceEndpoint.Send(new InvoiceOrder 
        { 
            OrderId = orderId,
            InvoiceId = Guid.NewGuid()
        });
        Console.WriteLine($"  💰 Order {orderId.ToString()[..8]}... Invoiced");
        invoiced++;
        await Task.Delay(delay);
    }

    // ===== Summary =====
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("  SIMULATION COMPLETE");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine($"  📦 Orders created:     {orders}");
    Console.WriteLine($"  🔄 Started processing: {processed}");
    Console.WriteLine($"  🚚 Shipped:            {shipped}");
    Console.WriteLine($"  📬 Delivered:          {delivered}");
    Console.WriteLine($"  💰 Invoiced:           {invoiced}");
    Console.WriteLine($"  ❌ Cancelled:          {cancelled}");
    Console.WriteLine();

    await busControl.StopAsync();
    Console.WriteLine("✅ Simulation finished!");

}, ordersOption, customersOption, simulateWorkflowOption, delayOption, rabbitHostOption, rabbitUserOption, rabbitPasswordOption, customersApiOption);

return await rootCommand.InvokeAsync(args);

// ===== DTOs for Customers API =====
record CreateCustomerRequestDto
{
    public string CompanyName { get; init; } = "";
    public string? DisplayName { get; init; }
    public string Email { get; init; } = "";
    public string? Phone { get; init; }
    public string? TaxId { get; init; }
    public string? VatNumber { get; init; }
    public string PreferredLanguage { get; init; } = "en";
    public string PreferredCurrency { get; init; } = "EUR";
    public string? Notes { get; init; }
}

record CreateCustomerResponseDto(Guid Id);

record CustomerSummaryDto(Guid Id, string CompanyName, string? DisplayName, string Email, DateTime CreatedAt, bool IsActive);

// ===== DTOs for Ordering API =====
record OrderSummary(Guid Id, Guid CustomerId, string? CustomerReference, int Status, DateTime CreatedAt, decimal GrandTotal, int LineCount);
