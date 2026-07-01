namespace OrderService.Domain;

public class Order
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Payment state. New orders start as Pending (the enum's default value).
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Amount { get; set; }
    public string? TxnRef { get; set; }
    public string? VnpTransactionNo { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}
