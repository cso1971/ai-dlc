using Contracts.Commands.Ordering;
using Contracts.ValueObjects.Ordering;
using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Api.Domain;
using Ordering.Api.Services;

namespace Ordering.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders");

        // GET /api/orders
        group.MapGet("/", GetAllOrders)
            .WithName("GetAllOrders")
            .WithSummary("Get all orders")
            .WithDescription("Returns a list of all orders with summary information")
            .Produces<List<OrderSummaryResponse>>(StatusCodes.Status200OK);

        // GET /api/orders/stats
        group.MapGet("/stats", GetOrderStats)
            .WithName("GetOrderStats")
            .WithSummary("Get order aggregates")
            .WithDescription("Returns total order count and total value per currency (for AI chatbot).")
            .Produces<OrderStatsResponse>(StatusCodes.Status200OK);

        // GET /api/orders/{id}
        group.MapGet("/{id:guid}", GetOrderById)
            .WithName("GetOrderById")
            .WithSummary("Get order by ID")
            .WithDescription("Returns the complete details of a specific order including all line items")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/orders
        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithSummary("Create a new order")
            .WithDescription("Creates a new order with the specified line items. The order starts in 'Created' status.")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        // POST /api/orders/{id}/start-processing
        group.MapPost("/{id:guid}/start-processing", StartProcessing)
            .WithName("StartOrderProcessing")
            .WithSummary("Start processing an order")
            .WithDescription("Transitions the order from 'Created' to 'InProgress' status")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/orders/{id}/ship
        group.MapPost("/{id:guid}/ship", ShipOrder)
            .WithName("ShipOrder")
            .WithSummary("Ship an order")
            .WithDescription("Transitions the order from 'InProgress' to 'Shipped' status. Requires tracking number.")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/orders/{id}/deliver
        group.MapPost("/{id:guid}/deliver", DeliverOrder)
            .WithName("DeliverOrder")
            .WithSummary("Mark order as delivered")
            .WithDescription("Transitions the order from 'Shipped' to 'Delivered' status")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/orders/{id}/invoice
        group.MapPost("/{id:guid}/invoice", InvoiceOrder)
            .WithName("InvoiceOrder")
            .WithSummary("Mark order as invoiced")
            .WithDescription("Transitions the order from 'Delivered' to 'Invoiced' status (final state)")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/orders/{id}/cancel
        group.MapPost("/{id:guid}/cancel", CancelOrder)
            .WithName("CancelOrder")
            .WithSummary("Cancel an order")
            .WithDescription("Cancels the order. Can be called from any status except 'Invoiced'. Requires a cancellation reason.")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetAllOrders(OrderingService service)
    {
        var orders = await service.GetAllOrdersAsync();
        var response = orders.Select(MapToSummary).ToList();
        return Results.Ok(response);
    }

    private static async Task<IResult> GetOrderStats(OrderingService service, CancellationToken cancellationToken)
    {
        var stats = await service.GetOrderStatsAsync(cancellationToken);
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetOrderById(Guid id, OrderingService service)
    {
        var order = await service.GetOrderAsync(id);
        if (order == null)
            return Results.NotFound(new { Message = $"Order {id} not found" });

        return Results.Ok(MapToResponse(order));
    }

    private static async Task<IResult> CreateOrder(CreateOrderRequest request, OrderingService service, CancellationToken cancellationToken)
    {
        var command = new CreateOrder
        {
            CustomerId = request.CustomerId,
            CustomerReference = request.CustomerReference,
            RequestedDeliveryDate = request.RequestedDeliveryDate,
            Priority = request.Priority,
            CurrencyCode = request.CurrencyCode,
            PaymentTerms = request.PaymentTerms,
            ShippingMethod = request.ShippingMethod,
            ShippingAddress = request.ShippingAddress != null ? MapToShippingAddress(request.ShippingAddress) : null,
            Notes = request.Notes,
            Lines = request.Lines.Select(l => new OrderLineItem
            {
                ProductCode = l.ProductCode,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitOfMeasure = l.UnitOfMeasure,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxPercent = l.TaxPercent
            }).ToList()
        };

        try
        {
            var order = await service.CreateOrderAsync(command, cancellationToken);
            return Results.Created($"/api/orders/{order.Id}", MapToResponse(order));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static async Task<IResult> StartProcessing(Guid id, StartProcessingRequest? request, OrderingService service)
    {
        var command = new StartOrderProcessing
        {
            OrderId = id,
            Notes = request?.Notes
        };

        try
        {
            var order = await service.StartProcessingAsync(command);
            return Results.Ok(MapToResponse(order));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static async Task<IResult> ShipOrder(Guid id, ShipOrderRequest request, OrderingService service)
    {
        var command = new ShipOrder
        {
            OrderId = id,
            TrackingNumber = request.TrackingNumber,
            Carrier = request.Carrier,
            EstimatedDeliveryDate = request.EstimatedDeliveryDate
        };

        try
        {
            var order = await service.ShipOrderAsync(command);
            return Results.Ok(MapToResponse(order));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static async Task<IResult> DeliverOrder(Guid id, DeliverOrderRequest? request, OrderingService service)
    {
        var command = new DeliverOrder
        {
            OrderId = id,
            ReceivedBy = request?.ReceivedBy,
            DeliveryNotes = request?.DeliveryNotes
        };

        try
        {
            var order = await service.DeliverOrderAsync(command);
            return Results.Ok(MapToResponse(order));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static async Task<IResult> InvoiceOrder(Guid id, InvoiceOrderRequest? request, OrderingService service)
    {
        var command = new InvoiceOrder
        {
            OrderId = id,
            InvoiceId = request?.InvoiceId
        };

        try
        {
            var order = await service.InvoiceOrderAsync(command);
            return Results.Ok(MapToResponse(order));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static async Task<IResult> CancelOrder(Guid id, CancelOrderRequest request, OrderingService service)
    {
        var command = new Contracts.Commands.Ordering.CancelOrder
        {
            OrderId = id,
            CancellationReason = request.CancellationReason
        };

        try
        {
            var order = await service.CancelOrderAsync(command);
            return Results.Ok(MapToResponse(order));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    // Mapping helpers
    private static Contracts.ValueObjects.Ordering.ShippingAddress MapToShippingAddress(ShippingAddressDto dto) => new()
    {
        RecipientName = dto.RecipientName,
        AddressLine1 = dto.AddressLine1,
        AddressLine2 = dto.AddressLine2,
        City = dto.City,
        StateOrProvince = dto.StateOrProvince,
        PostalCode = dto.PostalCode,
        CountryCode = dto.CountryCode,
        PhoneNumber = dto.PhoneNumber,
        Notes = dto.Notes
    };

    private static OrderResponse MapToResponse(Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        CustomerReference = order.CustomerReference,
        RequestedDeliveryDate = order.RequestedDeliveryDate,
        Priority = order.Priority,
        CurrencyCode = order.CurrencyCode,
        PaymentTerms = order.PaymentTerms,
        ShippingMethod = order.ShippingMethod,
        ShippingAddress = order.ShippingAddress != null ? new ShippingAddressDto
        {
            RecipientName = order.ShippingAddress.RecipientName,
            AddressLine1 = order.ShippingAddress.AddressLine1,
            AddressLine2 = order.ShippingAddress.AddressLine2,
            City = order.ShippingAddress.City,
            StateOrProvince = order.ShippingAddress.StateOrProvince,
            PostalCode = order.ShippingAddress.PostalCode,
            CountryCode = order.ShippingAddress.CountryCode,
            PhoneNumber = order.ShippingAddress.PhoneNumber,
            Notes = order.ShippingAddress.Notes
        } : null,
        Notes = order.Notes,
        Status = order.Status,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        TrackingNumber = order.TrackingNumber,
        Carrier = order.Carrier,
        EstimatedDeliveryDate = order.EstimatedDeliveryDate,
        ShippedAt = order.ShippedAt,
        DeliveredAt = order.DeliveredAt,
        ReceivedBy = order.ReceivedBy,
        DeliveryNotes = order.DeliveryNotes,
        InvoiceId = order.InvoiceId,
        InvoicedAt = order.InvoicedAt,
        CancellationReason = order.CancellationReason,
        CancelledAt = order.CancelledAt,
        Lines = order.Lines.Select(l => new OrderLineResponse
        {
            Id = l.Id,
            LineNumber = l.LineNumber,
            ProductCode = l.ProductCode,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitOfMeasure = l.UnitOfMeasure,
            UnitPrice = l.UnitPrice,
            DiscountPercent = l.DiscountPercent,
            TaxPercent = l.TaxPercent,
            LineTotal = l.LineTotal,
            TaxAmount = l.TaxAmount,
            LineTotalWithTax = l.LineTotalWithTax
        }).ToList(),
        Subtotal = order.Subtotal,
        TotalTax = order.TotalTax,
        GrandTotal = order.GrandTotal
    };

    private static OrderSummaryResponse MapToSummary(Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        CustomerReference = order.CustomerReference,
        Status = order.Status,
        CreatedAt = order.CreatedAt,
        GrandTotal = order.GrandTotal,
        LineCount = order.Lines.Count
    };
}
