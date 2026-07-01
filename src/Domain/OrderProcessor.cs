using OrderService.Contracts;

namespace OrderService.Domain;

/// <summary>
/// Pure, I/O-free domain logic for placing an order. Kept separate from the
/// HTTP and persistence layers so it can be unit tested with no mocks/infra.
/// </summary>
public static class OrderProcessor
{
    public static (Order order, OrderPlaced @event) Place(PlaceOrderRequest request, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
            throw new ArgumentException("Sku is required", nameof(request));
        if (request.Quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(request));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            Sku = request.Sku,
            Quantity = request.Quantity,
            CreatedAt = now,
            PaymentStatus = PaymentStatus.Pending
        };

        var @event = new OrderPlaced
        {
            Version = "v1",
            OrderId = order.Id,
            Sku = order.Sku,
            Quantity = order.Quantity,
            PlacedAt = now
        };

        return (order, @event);
    }
}
