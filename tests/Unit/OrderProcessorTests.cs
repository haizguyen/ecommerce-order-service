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
        var request = new PlaceOrderRequest("SKU-001", 3);

        var (order, @event) = OrderProcessor.Place(request, now);

        Assert.Equal(order.Id, @event.OrderId);
        Assert.Equal("SKU-001", @event.Sku);
        Assert.Equal(3, @event.Quantity);
        Assert.Equal("v1", @event.Version);
        Assert.Equal(now, @event.PlacedAt);
        Assert.NotEqual(Guid.Empty, order.Id);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("  ", 1)]
    [InlineData("SKU-001", 0)]
    [InlineData("SKU-001", -2)]
    public void Place_RejectsInvalidRequests(string sku, int quantity)
    {
        var request = new PlaceOrderRequest(sku, quantity);
        Assert.Throws<ArgumentException>(() => OrderProcessor.Place(request, DateTimeOffset.UtcNow));
    }
}
