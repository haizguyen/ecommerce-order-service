namespace OrderService.Domain;

public class Order
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "AUD";
    public string Status { get; set; } = "placed";
    public DateTimeOffset CreatedAt { get; set; }
}
