namespace OrderService.Domain;

/// <summary>
/// Lifecycle of an order's payment. New orders start as <see cref="Pending"/>.
/// </summary>
public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Cancelled
}
