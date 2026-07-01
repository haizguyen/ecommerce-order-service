using System;
using System.Collections.Generic;
using OrderService.Services;
using Xunit;

namespace OrderService.Unit.Tests;

[Trait("Category", "Unit")]
public class VNPayServiceTests
{
    // Fixed merchant config used across the sample vectors below. The HashSecret
    // is a test-only key; the resulting hashes were cross-checked against openssl
    // and python HMAC-SHA512 on the identical canonical string, so these vectors
    // verify conformance with the VNPay 2.1.0 spec rather than just this code.
    private const string TmnCode = "2QXUI4B4";
    private const string HashSecret = "SECRETKEY0123456789ABCDEFHIJKLMN";
    private const string ReturnUrl = "https://merchant.example.com/vnpay/return";
    private const string BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    private static VNPayService CreateService() => new(new VNPayOptions
    {
        TmnCode = TmnCode,
        HashSecret = HashSecret,
        ReturnUrl = ReturnUrl,
    });

    private static VNPayPaymentRequest SampleRequest() => new()
    {
        Amount = 10_000, // VND -> vnp_Amount = 1000000
        TxnRef = "20240101153000",
        OrderInfo = "Thanh toan don hang 20240101153000",
        IpAddress = "127.0.0.1",
        CreateDate = new DateTimeOffset(2024, 1, 1, 15, 30, 0, TimeSpan.FromHours(7)),
        OrderType = "other",
    };

    [Fact]
    public void BuildPaymentUrl_ProducesSortedEncodedSignedUrl_MatchingSampleVector()
    {
        // Parameters are ordinal-sorted by name, URL-encoded (space -> '+',
        // "://" -> "%3A%2F%2F"), and terminated with the HMAC-SHA512 secure hash.
        const string expected =
            "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html" +
            "?vnp_Amount=1000000" +
            "&vnp_Command=pay" +
            "&vnp_CreateDate=20240101153000" +
            "&vnp_CurrCode=VND" +
            "&vnp_IpAddr=127.0.0.1" +
            "&vnp_Locale=vn" +
            "&vnp_OrderInfo=Thanh+toan+don+hang+20240101153000" +
            "&vnp_OrderType=other" +
            "&vnp_ReturnUrl=https%3A%2F%2Fmerchant.example.com%2Fvnpay%2Freturn" +
            "&vnp_TmnCode=2QXUI4B4" +
            "&vnp_TxnRef=20240101153000" +
            "&vnp_Version=2.1.0" +
            "&vnp_SecureHash=5cb0461e4a2d3c670c15ab2f47ec62692b1ea5d97f09e9dd1c5044864644621057c06f9ee7f0138a2350bd1ac46ffcf0c6873be053cba2fed24d988624dad93f";

        var url = CreateService().BuildPaymentUrl(BaseUrl, SampleRequest());

        Assert.Equal(expected, url);
    }

    [Fact]
    public void BuildPaymentUrl_MultipliesAmountByOneHundred_PerSpec()
    {
        var request = SampleRequest() with { Amount = 250_000 };

        var url = CreateService().BuildPaymentUrl(BaseUrl, request);

        Assert.Contains("vnp_Amount=25000000&", url);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void BuildPaymentUrl_RejectsNonPositiveAmount(long amount)
    {
        var request = SampleRequest() with { Amount = amount };
        Assert.Throws<ArgumentException>(() => CreateService().BuildPaymentUrl(BaseUrl, request));
    }

    [Fact]
    public void VerifySignature_ReturnsTrue_ForDocumentedSampleCallback()
    {
        // A VNPay Return/IPN callback (decoded values) with a secure hash computed
        // independently via python HMAC-SHA512 over the canonical response string.
        var callback = SampleCallback(
            "72b706b50b2ef722010c6d051e8a18f2f3d494dc94b36a6b093e71218bbb13c5" +
            "4eea0ff11fda8c6f7d0c622b904fb7b00aae95b75af4289bdd8df1033756f5cd");

        Assert.True(CreateService().VerifySignature(callback));
    }

    [Fact]
    public void VerifySignature_IgnoresEmptyFields_WhenRecomputingHash()
    {
        // Empty-valued fields (and vnp_SecureHashType) must be excluded from the
        // signed data, so adding them does not change the verification result.
        var callback = SampleCallback(
            "72b706b50b2ef722010c6d051e8a18f2f3d494dc94b36a6b093e71218bbb13c5" +
            "4eea0ff11fda8c6f7d0c622b904fb7b00aae95b75af4289bdd8df1033756f5cd");
        callback["vnp_SecureHashType"] = "";
        callback["vnp_Message"] = "";

        Assert.True(CreateService().VerifySignature(callback));
    }

    [Fact]
    public void VerifySignature_ReturnsFalse_WhenSignatureTampered()
    {
        var callback = SampleCallback(
            "72b706b50b2ef722010c6d051e8a18f2f3d494dc94b36a6b093e71218bbb13c5" +
            "4eea0ff11fda8c6f7d0c622b904fb7b00aae95b75af4289bdd8df1033756f5cd");
        // Attacker inflates the amount but cannot re-sign without the secret.
        callback["vnp_Amount"] = "999999999";

        Assert.False(CreateService().VerifySignature(callback));
    }

    [Fact]
    public void VerifySignature_ReturnsFalse_WhenHashMissing()
    {
        var callback = SampleCallback("ignored");
        callback.Remove("vnp_SecureHash");

        Assert.False(CreateService().VerifySignature(callback));
    }

    [Fact]
    public void BuildThenVerify_RoundTrips()
    {
        var service = CreateService();
        var url = service.BuildPaymentUrl(BaseUrl, SampleRequest());

        // Parse the query string back into decoded parameters, as a callback handler would.
        var query = url[(url.IndexOf('?') + 1)..];
        var parsed = new Dictionary<string, string>();
        foreach (var pair in query.Split('&'))
        {
            var eq = pair.IndexOf('=');
            parsed[pair[..eq]] = System.Net.WebUtility.UrlDecode(pair[(eq + 1)..]);
        }

        Assert.True(service.VerifySignature(parsed));
    }

    private static Dictionary<string, string> SampleCallback(string secureHash) => new()
    {
        ["vnp_Amount"] = "1000000",
        ["vnp_BankCode"] = "NCB",
        ["vnp_BankTranNo"] = "VNP14422574",
        ["vnp_CardType"] = "ATM",
        ["vnp_OrderInfo"] = "Thanh toan don hang 20240101153000",
        ["vnp_PayDate"] = "20240101153500",
        ["vnp_ResponseCode"] = "00",
        ["vnp_TmnCode"] = TmnCode,
        ["vnp_TransactionNo"] = "14422574",
        ["vnp_TransactionStatus"] = "00",
        ["vnp_TxnRef"] = "20240101153000",
        ["vnp_SecureHash"] = secureHash,
    };
}
