namespace OrderService.Domain;

/// <summary>
/// The already-verified outcome of a VNPay payment callback (IPN or return URL).
/// Signature validation happens in the infrastructure layer; by the time a value
/// reaches the domain it is trusted, so this carries only the fields the payment
/// transition needs.
/// </summary>
/// <param name="ResponseCode">VNPay <c>vnp_ResponseCode</c>. <c>"00"</c> = success, <c>"24"</c> = customer cancelled, anything else = failure.</param>
/// <param name="TransactionNo">VNPay <c>vnp_TransactionNo</c>, recorded on success.</param>
/// <param name="Amount">Amount reported by VNPay, validated against the order total.</param>
public record VnpayResult(string ResponseCode, string TransactionNo, decimal Amount);
