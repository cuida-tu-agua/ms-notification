using System.Text.Json;
using Microsoft.Extensions.Options;
using SyWater.Notifications.Application.Events;
using SyWater.Notifications.Application.Ports.In;

namespace SyWater.Notifications.Api.Messaging;

/// <summary>
/// Queue "notification.device-events": device.linked / device.unlinked (tell the owner and learn who owns the
/// place), device.valve.reported (a valve that closed is a critical notification, HU-033) and
/// device.reading.received (proof of life for the "sensor disconnected" alert, HU-032).
/// </summary>
public sealed class DeviceEventsConsumer(
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options,
    ILogger<DeviceEventsConsumer> logger) : RabbitMqEventConsumer(options, logger)
{
    protected override string QueueName => "notification.device-events";

    protected override IReadOnlyList<string> RoutingKeys =>
        [DeviceLinkedEvent.EventType, DeviceUnlinkedEvent.EventType, ValveReportedEvent.EventType, ReadingReceivedEvent.EventType];

    protected override async Task HandleAsync(string eventId, string type, JsonElement data, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();   // one DbContext per message
        var handler = scope.ServiceProvider.GetRequiredService<IDeviceEventsHandler>();

        switch (type)
        {
            case DeviceLinkedEvent.EventType:
                var linked = Read<DeviceLinkedEvent>(data);
                RequireIds(linked.DeviceId, linked.PlaceId, linked.UserId);
                await handler.HandleAsync(eventId, linked, ct);
                break;
            case DeviceUnlinkedEvent.EventType:
                var unlinked = Read<DeviceUnlinkedEvent>(data);
                RequireIds(unlinked.DeviceId, unlinked.PlaceId, unlinked.UserId);
                await handler.HandleAsync(eventId, unlinked, ct);
                break;
            case ValveReportedEvent.EventType:
                var reported = Read<ValveReportedEvent>(data);
                RequireIds(reported.DeviceId, reported.PlaceId);
                await handler.HandleAsync(eventId, reported, ct);
                break;
            case ReadingReceivedEvent.EventType:
                var reading = Read<ReadingReceivedEvent>(data);
                RequireIds(reading.DeviceId, reading.PlaceId);
                await handler.HandleAsync(eventId, reading, ct);
                break;
            default:
                logger.LogDebug("Event {Type} ignored", type);
                break;
        }
    }

    /// <summary>A missing id deserializes as Guid.Empty: that event can never be processed (dead-letter it).</summary>
    private static void RequireIds(params Guid[] ids)
    {
        if (ids.Any(id => id == Guid.Empty)) throw new JsonException("Event without deviceId/placeId/userId.");
    }

    private T Read<T>(JsonElement data) => data.Deserialize<T>(Json) ?? throw new JsonException($"Empty {typeof(T).Name}.");
}
