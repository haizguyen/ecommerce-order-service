using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OrderService.Services;

/// <summary>
/// Merchant-side configuration for the VNPay payment gateway. These values are
/// issued by VNPay (TmnCode / HashSecret) or fixed by the 2.1.0 spec.
/// </summary>
public sealed record VNPayOptions
{
    /// <summary>Terminal / merchant code issued by VNPay (vnp_TmnCode).</summary>
    public required string TmnCode { get; init; }

    /// <summary>Secret key used to sign requests with HMAC-SHA512 (never sent to the client).</summary>
    public required string HashSecret { get; init; }

    /// <summary>URL VNPay redirects the customer back to after payment (vnp_ReturnUrl).</summary>
    public required string ReturnUrl { get; init; }

    /// <summary>API version. Defaults to the 2.1.0 spec this service implements.</summary>
    public string Version { get; init; } = "2.1.0";

    /// <summary>Command for a pay request (vnp_Command).</summary>
    public string Command { get; init; } = "pay";

    /// <summary>Currency code. VNPay only supports VND.</summary>
    public string CurrCode { get; init; } = "VND";

    /// <summary>Display locale for the payment page (vn / en).</summary>
    public string Locale { get; init; } = "vn";
}

/// <summary>
/// The order-specific inputs needed to build a single VNPay pay URL. All values
/// are supplied by the caller so the signing logic stays free of clocks, HTTP
/// context and other I/O.
/// </summary>
public sealed record VNPayPaymentRequest
{
    /// <summary>Order amount in VND (whole dong). VNPay's vnp_Amount is this value * 100.</summary>
    public required long Amount { get; init; }

    /// <summary>Unique merchant transaction reference (vnp_TxnRef).</summary>
    public required string TxnRef { get; init; }

    /// <summary>Human readable order description (vnp_OrderInfo).</summary>
    public required string OrderInfo { get; init; }

    /// <summary>Customer IP address (vnp_IpAddr).</summary>
    public required string IpAddress { get; init; }

    /// <summary>
    /// Timestamp the request is created (vnp_CreateDate), formatted as yyyyMMddHHmmss.
    /// VNPay expects GMT+7 wall-clock time; the caller is responsible for passing a
    /// value already in that offset so this logic stays deterministic and I/O-free.
    /// </summary>
    public required DateTimeOffset CreateDate { get; init; }

    /// <summary>Order category (vnp_OrderType).</summary>
    public string OrderType { get; init; } = "other";

    /// <summary>Optional bank/method code (vnp_BankCode). Omitted from the request when empty.</summary>
    public string BankCode { get; init; } = string.Empty;
}

/// <summary>
/// Pure, I/O-free VNPay 2.1.0 signing logic. Mirrors <c>OrderProcessor</c>: no
/// HTTP, no config binding and no clock access, so it can be unit tested against
/// the documented sample vectors with no mocks or infrastructure.
/// </summary>
/// <remarks>
/// The canonical string, sort order, URL-encoding and HMAC-SHA512 hashing follow
/// VNPay's official C# <c>VnPayLibrary</c> exactly: parameters are ordered by an
/// ordinal comparison of their names, empty values are excluded, and both the key
/// and value are URL-encoded before being joined with <c>&amp;</c>.
/// </remarks>
public sealed class VNPayService
{
    private readonly VNPayOptions _options;

    public VNPayService(VNPayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Builds a fully signed VNPay redirect URL for the given order.
    /// </summary>
    /// <param name="baseUrl">The VNPay pay endpoint (e.g. the sandbox vnpayment URL).</param>
    /// <param name="request">The order-specific parameters.</param>
    /// <returns>The base URL with the sorted, encoded query string and vnp_SecureHash appended.</returns>
    public string BuildPaymentUrl(string baseUrl, VNPayPaymentRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(request));
        if (string.IsNullOrWhiteSpace(request.TxnRef))
            throw new ArgumentException("TxnRef is required", nameof(request));

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = _options.Version,
            ["vnp_Command"] = _options.Command,
            ["vnp_TmnCode"] = _options.TmnCode,
            // VNPay expects the amount in the smallest unit (dong * 100).
            ["vnp_Amount"] = (request.Amount * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = _options.CurrCode,
            ["vnp_TxnRef"] = request.TxnRef,
            ["vnp_OrderInfo"] = request.OrderInfo,
            ["vnp_OrderType"] = request.OrderType,
            ["vnp_Locale"] = _options.Locale,
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_IpAddr"] = request.IpAddress,
            ["vnp_CreateDate"] = request.CreateDate.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrEmpty(request.BankCode))
            fields["vnp_BankCode"] = request.BankCode;

        var signData = BuildCanonicalData(fields);
        var secureHash = HmacSha512(_options.HashSecret, signData);

        return $"{baseUrl}?{signData}&vnp_SecureHash={secureHash}";
    }

    /// <summary>
    /// Recomputes and compares the secure hash for an inbound Return/IPN callback.
    /// </summary>
    /// <param name="queryParameters">
    /// The decoded query parameters received from VNPay, including vnp_SecureHash.
    /// </param>
    /// <returns><c>true</c> when the recomputed HMAC-SHA512 matches the supplied hash.</returns>
    public bool VerifySignature(IReadOnlyDictionary<string, string> queryParameters)
    {
        ArgumentNullException.ThrowIfNull(queryParameters);

        if (!queryParameters.TryGetValue("vnp_SecureHash", out var providedHash)
            || string.IsNullOrEmpty(providedHash))
        {
            return false;
        }

        // The hash itself and the (optional) hash type are excluded from the data
        // that gets signed, exactly as on the request side.
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in queryParameters)
        {
            if (key is "vnp_SecureHash" or "vnp_SecureHashType")
                continue;
            fields[key] = value;
        }

        var signData = BuildCanonicalData(fields);
        var computedHash = HmacSha512(_options.HashSecret, signData);

        return FixedTimeHexEquals(computedHash, providedHash);
    }

    /// <summary>
    /// Joins the sorted parameters into VNPay's canonical <c>key=value</c> string.
    /// Empty values are skipped and both key and value are URL-encoded, matching
    /// the official library on both the request and response paths.
    /// </summary>
    private static string BuildCanonicalData(SortedDictionary<string, string> fields)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in fields)
        {
            if (string.IsNullOrEmpty(value))
                continue;
            if (sb.Length > 0)
                sb.Append('&');
            sb.Append(System.Net.WebUtility.UrlEncode(key));
            sb.Append('=');
            sb.Append(System.Net.WebUtility.UrlEncode(value));
        }
        return sb.ToString();
    }

    /// <summary>Computes a lower-case hex HMAC-SHA512 of <paramref name="data"/> keyed by <paramref name="key"/>.</summary>
    private static string HmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Case-insensitive, length-safe, constant-time comparison of two hex hashes.
    /// Guards signature checks against timing side channels.
    /// </summary>
    private static bool FixedTimeHexEquals(string computed, string provided)
    {
        var a = Encoding.ASCII.GetBytes(computed.ToLowerInvariant());
        var b = Encoding.ASCII.GetBytes(provided.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
