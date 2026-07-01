using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using OrderService.Configuration;
using Xunit;

namespace OrderService.Unit.Tests;

[Trait("Category", "Unit")]
public class VNPayOptionsTests
{
    private static readonly Dictionary<string, string?> AppSettingsDefaults = new()
    {
        ["VNPay:PaymentUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
        ["VNPay:Version"] = "2.1.0",
        ["VNPay:Command"] = "pay",
        ["VNPay:CurrCode"] = "VND",
        ["VNPay:Locale"] = "vn",
        ["VNPay:ReturnUrl"] = "",
        ["VNPay:IpnUrl"] = "",
        ["VNPay:TmnCode"] = "",
        ["VNPay:HashSecret"] = "",
    };

    [Fact]
    public void Binds_NonSecretDefaults_FromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(AppSettingsDefaults)
            .Build();

        var options = config.GetSection(VNPayOptions.SectionName).Get<VNPayOptions>();

        Assert.NotNull(options);
        Assert.Equal("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", options!.PaymentUrl);
        Assert.Equal("2.1.0", options.Version);
        Assert.Equal("pay", options.Command);
        Assert.Equal("VND", options.CurrCode);
        Assert.Equal("vn", options.Locale);
    }

    [Fact]
    public void LeavesSecrets_Empty_WhenNotOverridden()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(AppSettingsDefaults)
            .Build();

        var options = config.GetSection(VNPayOptions.SectionName).Get<VNPayOptions>()!;

        Assert.Equal(string.Empty, options.TmnCode);
        Assert.Equal(string.Empty, options.HashSecret);
    }

    [Fact]
    public void EnvironmentVariables_OverrideSecrets()
    {
        // Simulate the VNPay__TmnCode / VNPay__HashSecret env-var override convention.
        var envVars = new Dictionary<string, string?>
        {
            ["VNPay__TmnCode"] = "MERCHANT123",
            ["VNPay__HashSecret"] = "supersecret",
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(AppSettingsDefaults)
            .AddInMemoryCollection(NormalizeEnvKeys(envVars))
            .Build();

        var options = config.GetSection(VNPayOptions.SectionName).Get<VNPayOptions>()!;

        Assert.Equal("MERCHANT123", options.TmnCode);
        Assert.Equal("supersecret", options.HashSecret);
        // Non-secret defaults remain intact after the override.
        Assert.Equal("VND", options.CurrCode);
    }

    // The environment-variable provider translates "__" into the ":" section separator.
    // We apply the same translation over in-memory keys to exercise identical binding
    // behavior without mutating real process environment variables.
    private static Dictionary<string, string?> NormalizeEnvKeys(Dictionary<string, string?> envVars)
    {
        var result = new Dictionary<string, string?>();
        foreach (var (key, value) in envVars)
        {
            result[key.Replace("__", ":")] = value;
        }

        return result;
    }
}
