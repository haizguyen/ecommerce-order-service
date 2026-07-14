using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderService.Diagnostics;
using Xunit;

namespace OrderService.Unit.Tests;

[Trait("Category", "Unit")]
public class DiagnosticsInfoTests
{
    [Fact]
    public void ServiceName_IsNonEmpty()
    {
        var name = DiagnosticsInfo.ServiceName;
        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public void GetCurrentUtc_ReturnsValidIso8601()
    {
        var timestamp = DiagnosticsInfo.GetCurrentUtc();
        var parsed = DateTimeOffset.Parse(timestamp);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
    }
}

[Trait("Category", "Integration")]
public class DiagnosticsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DiagnosticsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("environment", "Test");
        }).CreateClient();
    }

    [Fact]
    public async Task GetDiagnosticsInfo_Returns200WithServiceNameAndCurrentUtc()
    {
        var response = await _client.GetAsync("/diagnostics/info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var serviceName = root.GetProperty("serviceName").GetString();
        Assert.False(string.IsNullOrWhiteSpace(serviceName));

        var currentUtc = root.GetProperty("currentUtc").GetString();
        Assert.NotNull(currentUtc);
        var parsed = DateTimeOffset.Parse(currentUtc);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
    }

    [Fact]
    public async Task GetDiagnosticsPing_Returns200WithPong()
    {
        var response = await _client.GetAsync("/diagnostics/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("pong", body);
    }
}
