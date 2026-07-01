namespace OrderService.Domain;

/// <summary>Payment lifecycle state for an <see cref="Order"/>.</summary>
public enum PaymentStatus
{
    /// <summary>Awaiting a payment result. Initial state.</summary>
    Pending = 0,

    /// <summary>Payment succeeded. Terminal.</summary>
    Paid = 1,

    /// <summary>Payment failed (declined, error, expired, ...). Terminal.</summary>
    Failed = 2,

    /// <summary>Customer cancelled the payment. Terminal.</summary>
    Cancelled = 3
}
