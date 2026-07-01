namespace OrderService.Domain;

public class Order
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Total amount owed for the order, matched against the VNPay callback.</summary>
    public decimal Amount { get; set; }

    /// <summary>Payment lifecycle state. Starts <see cref="PaymentStatus.Pending"/>.</summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    /// <summary>Set once, on the first successful Pending -> Paid transition.</summary>
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>VNPay transaction number recorded on successful payment.</summary>
    public string? VnpTransactionNo { get; set; }
}
