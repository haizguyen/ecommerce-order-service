using System;
using OrderService.Domain;
using Xunit;

namespace OrderService.Unit.Tests;

[Trait("Category", "Unit")]
public class OrderProcessorTests
{
    [Fact]
    public void Place_BuildsOrderPlacedEvent_MatchingTheRequest()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new PlaceOrderRequest("SKU-001", 3, "Widget", 19.99m, "AUD");

        var (order, @event) = OrderProcessor.Place(request, now);

        Assert.Equal(order.Id, @event.OrderId);
        Assert.Equal("SKU-001", @event.Sku);
        Assert.Equal(3, @event.Quantity);
        Assert.Equal("v1", @event.Version);
        Assert.Equal(now, @event.PlacedAt);
        Assert.NotEqual(Guid.Empty, order.Id);
    }

    [Fact]
    public void Place_SnapshotsNewFieldsOntoOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new PlaceOrderRequest("SKU-002", 5, "Gadget", 9.95m, "USD");

        var (order, _) = OrderProcessor.Place(request, now);

        Assert.Equal(request.Name, order.Name);
        Assert.Equal(request.UnitPrice, order.UnitPrice);
        Assert.Equal(request.Currency, order.Currency);
        Assert.Equal("placed", order.Status);
        Assert.NotEqual(Guid.Empty, order.Id);
    }

    [Fact]
    public void Place_EventContractRemainsV1()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new PlaceOrderRequest("SKU-003", 1, "Test", 1.00m, "AUD");

        var (_, @event) = OrderProcessor.Place(request, now);

        Assert.Equal("v1", @event.Version);
        // Only v1 fields are present — structurally enforced by the record type.
        // Verify the event does NOT carry the new domain fields.
        var props = @event.GetType().GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("Name", props);
        Assert.DoesNotContain("UnitPrice", props);
        Assert.DoesNotContain("Currency", props);
        Assert.DoesNotContain("Status", props);
    }

    [Theory]
    [InlineData("", 1, "Widget", 10.0, "AUD")]
    [InlineData("  ", 1, "Widget", 10.0, "AUD")]
    [InlineData("SKU-001", 0, "Widget", 10.0, "AUD")]
    [InlineData("SKU-001", -2, "Widget", 10.0, "AUD")]
    public void Place_RejectsInvalidRequests(string sku, int quantity, string name, decimal unitPrice, string currency)
    {
        var request = new PlaceOrderRequest(sku, quantity, name, unitPrice, currency);
        Assert.Throws<ArgumentException>(() => OrderProcessor.Place(request, DateTimeOffset.UtcNow));
    }
}
