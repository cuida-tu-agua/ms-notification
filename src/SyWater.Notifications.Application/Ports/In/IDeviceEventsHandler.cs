using SyWater.Notifications.Application.Events;

namespace SyWater.Notifications.Application.Ports.In;

/// <summary>
/// Events from device-service (RabbitMQ). <paramref name="eventId"/> is the id of the envelope: it makes a
/// re-delivered event create no second notification.
/// </summary>
public interface IDeviceEventsHandler
{
    Task HandleAsync(string eventId, DeviceLinkedEvent e, CancellationToken ct);
    Task HandleAsync(string eventId, DeviceUnlinkedEvent e, CancellationToken ct);
    Task HandleAsync(string eventId, ValveReportedEvent e, CancellationToken ct);
    Task HandleAsync(string eventId, ReadingReceivedEvent e, CancellationToken ct);
}
