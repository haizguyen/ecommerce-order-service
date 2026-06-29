using System.Text.Json;
using Azure.Messaging.ServiceBus;
using OrderService.Contracts;

namespace OrderService.Messaging;

public class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    public const string TopicName = "orders";

    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public ServiceBusEventPublisher(ServiceBusClient client)
    {
        _client = client;
        _sender = client.CreateSender(TopicName);
    }

    public async Task PublishAsync(OrderPlaced @event, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(@event);
        var message = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            Subject = nameof(OrderPlaced),
            MessageId = @event.OrderId.ToString()
        };
        message.ApplicationProperties["version"] = @event.Version;
        await _sender.SendMessageAsync(message, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
