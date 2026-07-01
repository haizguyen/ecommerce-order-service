namespace OrderService.Configuration;

/// <summary>
/// Strongly-typed VNPay merchant and endpoint settings, bound from the "VNPay"
/// configuration section. Non-secret defaults live in appsettings.json;
/// <see cref="TmnCode"/> and <see cref="HashSecret"/> are supplied at runtime via
/// environment variables (<c>VNPay__TmnCode</c>, <c>VNPay__HashSecret</c>) or user-secrets.
/// </summary>
public class VNPayOptions
{
    /// <summary>Configuration section name this options type binds to.</summary>
    public const string SectionName = "VNPay";

    /// <summary>VNPay payment gateway URL orders are redirected to.</summary>
    public string PaymentUrl { get; set; } = string.Empty;

    /// <summary>VNPay API version (e.g. "2.1.0").</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>VNPay command (e.g. "pay").</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Currency code (e.g. "VND").</summary>
    public string CurrCode { get; set; } = string.Empty;

    /// <summary>Locale for the payment page (e.g. "vn").</summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>URL the user is returned to after payment.</summary>
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>URL VNPay calls server-to-server with the payment result (IPN).</summary>
    public string IpnUrl { get; set; } = string.Empty;

    /// <summary>Merchant terminal code. Secret — supplied via env var or user-secrets.</summary>
    public string TmnCode { get; set; } = string.Empty;

    /// <summary>Secret used to sign/verify VNPay requests. Supplied via env var or user-secrets.</summary>
    public string HashSecret { get; set; } = string.Empty;
}
