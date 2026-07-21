using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Contracts;
using OrderService.Data;
using OrderService.Messaging;
using Xunit;

namespace OrderService.Integration.Tests;

[Trait("Category", "Integration")]
public class OrderEndpointsTests
{
    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"OrdersTest_{Guid.NewGuid():N}";
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace SQL Server with InMemory database.
                var descriptorsToRemove = services.Where(d =>
                    d.ServiceType == typeof(OrdersDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<OrdersDbContext>)).ToList();
                foreach (var d in descriptorsToRemove)
                    services.Remove(d);

                services.AddDbContext<OrdersDbContext>(opt =>
                    opt.UseInMemoryDatabase(dbName));

                // Replace ServiceBus publisher with a no-op stub.
                var pubDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IEventPublisher));
                if (pubDescriptor is not null)
                    services.Remove(pubDescriptor);
                services.AddSingleton<IEventPublisher, StubEventPublisher>();

                // Replace ServiceBusClient registration so it doesn't try to connect.
                var sbDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(Azure.Messaging.ServiceBus.ServiceBusClient));
                if (sbDescriptor is not null)
                    services.Remove(sbDescriptor);
                services.AddSingleton(_ =>
                    new Azure.Messaging.ServiceBus.ServiceBusClient("Endpoint=sb://unused;SharedAccessKeyName=unused;SharedAccessKey=unused;"));
            });
        });
    }

    [Fact]
    public async Task GetOrders_EmptyTable_ReturnsEmptyArray()
    {
        // AC3: GET /orders empty table returns []
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderDto[]>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task PostAndGetOrders_ReturnsUiShapeWithTotal()
    {
        // AC1 + AC2: POST persists the snapshot, GET returns UI-shaped Order[] with total = unitPrice * quantity
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/orders", new
        {
            sku = "SKU-001",
            quantity = 3,
            name = "Widget",
            unitPrice = 19.99m,
            currency = "AUD"
        });

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var created = await postResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("SKU-001", created.Sku);
        Assert.Equal(3, created.Quantity);

        // GET /orders — verify UI shape
        var getResponse = await client.GetAsync("/orders");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var orders = await getResponse.Content.ReadFromJsonAsync<OrderDto[]>();
        Assert.NotNull(orders);
        var order = Assert.Single(orders);
        Assert.Equal(created.Id.ToString(), order.Id);
        Assert.Equal("placed", order.Status);
        Assert.Equal("AUD", order.Currency);
        Assert.Equal(19.99m * 3, order.Total);

        Assert.NotNull(order.Lines);
        var line = Assert.Single(order.Lines);
        Assert.Equal("SKU-001", line.Sku);
        Assert.Equal("Widget", line.Name);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(19.99m, line.UnitPrice);

        Assert.NotNull(order.PlacedAt);
        // Verify ISO 8601 format
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", order.PlacedAt);
    }

    [Fact]
    public async Task PostOrders_BlankSku_Returns400()
    {
        // AC4: POST /orders with blank sku → 400 { error }
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new
        {
            sku = "",
            quantity = 1,
            name = "Widget",
            unitPrice = 10.0m,
            currency = "AUD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Error));
    }

    [Fact]
    public async Task PostOrders_QuantityZero_Returns400()
    {
        // AC4: POST /orders with quantity = 0 → 400 { error }
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new
        {
            sku = "SKU-001",
            quantity = 0,
            name = "Widget",
            unitPrice = 10.0m,
            currency = "AUD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Error));
    }

    [Fact]
    public async Task PostOrders_NegativeQuantity_Returns400()
    {
        // AC4: POST /orders with negative quantity → 400 { error }
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", new
        {
            sku = "SKU-001",
            quantity = -2,
            name = "Widget",
            unitPrice = 10.0m,
            currency = "AUD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Error));
    }

    [Fact]
    public async Task GetOrderById_ReturnsSnapshotFields()
    {
        // AC1: After POST, GET /orders/{id} returns the new fields
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/orders", new
        {
            sku = "SKU-002",
            quantity = 5,
            name = "Gadget",
            unitPrice = 9.95m,
            currency = "USD"
        });

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var created = await postResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.NotNull(created);

        var getResponse = await client.GetAsync($"/orders/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var order = await getResponse.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);
        Assert.Equal("Gadget", order.Name);
        Assert.Equal(9.95m, order.UnitPrice);
        Assert.Equal("USD", order.Currency);
        Assert.Equal("placed", order.Status);
    }

    [Fact]
    public async Task GetOrderById_NonExistent_Returns404()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DTOs for deserializing responses

    private record CreateOrderResponse(Guid Id, string Sku, int Quantity);

    private record OrderDto(
        string Id,
        string Status,
        string PlacedAt,
        OrderLineDto[] Lines,
        decimal Total,
        string Currency);

    private record OrderLineDto(string Sku, string Name, int Quantity, decimal UnitPrice);

    private record Order(
        Guid Id,
        string Sku,
        int Quantity,
        string Name,
        decimal UnitPrice,
        string Currency,
        string Status,
        DateTimeOffset CreatedAt);

    private record ErrorResponse(string Error);
}

internal class StubEventPublisher : IEventPublisher
{
    public Task PublishAsync(OrderPlaced @event, CancellationToken ct = default)
        => Task.CompletedTask;
}
