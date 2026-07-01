namespace OrderService.Domain;

/// <summary>
/// Pure, I/O-free domain logic for applying a verified VNPay payment result to an
/// order. Mirrors <see cref="OrderProcessor"/>: no HTTP, no persistence, no mocks —
/// just a deterministic state transition that can be unit tested in isolation.
/// </summary>
public static class PaymentProcessor
{
    /// <summary>VNPay <c>vnp_ResponseCode</c> for a successful payment.</summary>
    private const string SuccessCode = "00";

    /// <summary>VNPay <c>vnp_ResponseCode</c> for a customer-cancelled payment.</summary>
    private const string CancelledCode = "24";

    /// <summary>
    /// Applies a verified VNPay result to <paramref name="order"/>, mutating its
    /// payment state, and returns the resulting status plus whether the
    /// <c>OrderPlaced</c> event should be published.
    /// </summary>
    /// <remarks>
    /// Idempotent: only a <see cref="PaymentStatus.Pending"/> -> <see cref="PaymentStatus.Paid"/>
    /// transition stamps <see cref="Order.PaidAt"/>/<see cref="Order.VnpTransactionNo"/> and
    /// signals publish. Re-applying to an already-<see cref="PaymentStatus.Paid"/> order is a
    /// no-op, so a duplicate/retried VNPay IPN never double-publishes the event.
    /// </remarks>
    /// <exception cref="ArgumentException">The callback amount does not match the order amount.</exception>
    public static PaymentTransition Apply(Order order, VnpayResult result, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(result);

        // Guard against tampered/mismatched callbacks before touching any state.
        if (result.Amount != order.Amount)
            throw new ArgumentException(
                $"Callback amount {result.Amount} does not match order amount {order.Amount}.",
                nameof(result));

        // Paid is terminal: a duplicate IPN is acknowledged but changes nothing and
        // must not publish OrderPlaced a second time.
        if (order.PaymentStatus == PaymentStatus.Paid)
            return new PaymentTransition(PaymentStatus.Paid, PublishOrderPlaced: false);

        if (result.ResponseCode == SuccessCode)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.PaidAt = now;
            order.VnpTransactionNo = result.TransactionNo;
            return new PaymentTransition(PaymentStatus.Paid, PublishOrderPlaced: true);
        }

        var status = result.ResponseCode == CancelledCode
            ? PaymentStatus.Cancelled
            : PaymentStatus.Failed;
        order.PaymentStatus = status;
        return new PaymentTransition(status, PublishOrderPlaced: false);
    }
}

/// <summary>
/// Outcome of <see cref="PaymentProcessor.Apply"/>: the order's resulting payment
/// status and whether the caller should publish the <c>OrderPlaced</c> event.
/// </summary>
public record PaymentTransition(PaymentStatus Status, bool PublishOrderPlaced);
