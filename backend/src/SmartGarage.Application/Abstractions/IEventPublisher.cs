using SmartGarage.Contracts.Events;

namespace SmartGarage.Application.Abstractions;

/// <summary>
/// Hands a message to the broker for the Worker to act on.
/// <para>
/// Publishing never throws. A message is sent after the business operation has been
/// committed, and a broker that is down must not turn a successful registration or booking
/// into an error. A failed publish is logged with everything needed to find it again.
/// </para>
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent message, string routingKey, CancellationToken ct)
        where TEvent : IntegrationEvent;
}
