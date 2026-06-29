using OrderService.Contracts;

namespace OrderService.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(OrderPlaced @event, CancellationToken ct = default);
}
