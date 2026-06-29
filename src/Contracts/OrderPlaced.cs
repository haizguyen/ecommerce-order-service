namespace OrderService.Contracts;

/// <summary>
/// Service Bus message published to the "orders" topic when an order is placed.
/// This is a versioned, immutable contract — adding a field is allowed; renaming
/// or removing requires a new message version + an updated Pact contract.
/// </summary>
public record OrderPlaced
{
    /// <summary>Message contract version.</summary>
    public string Version { get; init; } = "v1";

    public Guid OrderId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public DateTimeOffset PlacedAt { get; init; }
}
