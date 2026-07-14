using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OrderService.Data;
using OrderService.Diagnostics;
using OrderService.Domain;
using OrderService.Messaging;

// Propagate W3C trace context through Service Bus so a trace started by an HTTP
// request continues into the consuming service (distributed tracing).
AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

var builder = WebApplication.CreateBuilder(args);

// --- Observability: OpenTelemetry -> Azure Application Insights -------------
// Always tracks the Service Bus ActivitySource. Only exports to Azure Monitor
// when a connection string is configured, so local/E2E runs need no Azure.
var otel = builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Azure.Messaging.ServiceBus"));

var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(aiConnectionString))
{
    otel.UseAzureMonitor(o => o.ConnectionString = aiConnectionString);
}

builder.Services.AddDbContext<OrdersDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Db")));

builder.Services.AddSingleton(_ =>
    new ServiceBusClient(builder.Configuration.GetConnectionString("ServiceBus")));
builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Dev/E2E only: create the schema on startup so the stack is self-contained.
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "E2E")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    db.Database.EnsureCreated();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/diagnostics/info", () =>
    Results.Ok(new { serviceName = DiagnosticsInfo.ServiceName, currentUtc = DiagnosticsInfo.GetCurrentUtc() }));

app.MapGet("/diagnostics/ping", () => Results.Text("pong", "text/plain"));

app.MapPost("/orders", async (PlaceOrderRequest request, OrdersDbContext db, IEventPublisher publisher) =>
{
    Order order;
    try
    {
        var (created, @event) = OrderProcessor.Place(request, DateTimeOffset.UtcNow);
        order = created;
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await publisher.PublishAsync(@event);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    return Results.Created($"/orders/{order.Id}", new { order.Id, order.Sku, order.Quantity });
});

app.MapGet("/orders/{id:guid}", async (Guid id, OrdersDbContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.Run();

// Exposed so the test project can reference the assembly.
public partial class Program { }
