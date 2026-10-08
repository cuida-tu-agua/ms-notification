using SyWater.Notifications.Application.Events;
using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Devices;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.UseCases;

/// <summary>
/// Turns device events into notifications for the owner of the place. Every handler is idempotent:
/// RabbitMQ may deliver an event twice or out of order, and the result must be the same.
/// </summary>
public sealed class DeviceEventsHandler(IPlaceDeviceRepository devices, NotificationDispatcher dispatcher, TimeProvider clock)
    : IDeviceEventsHandler
{
    /// <summary>HU-012: the owner learns that the device is linked; and from now on we know who owns the place.</summary>
    public async Task HandleAsync(string eventId, DeviceLinkedEvent e, CancellationToken ct)
    {
        var now = Now();
        await devices.ReplaceAsync(PlaceDevice.ForNewLink(e.PlaceId, e.DeviceId, e.UserId, e.SerialNumber), now, ct);
        await Notify(e.UserId, NotificationType.DeviceLinked, NotificationSeverity.Info, "Dispositivo vinculado",
            $"El dispositivo {e.SerialNumber} quedó vinculado a tu lugar.", e.PlaceId, eventId, now, Label(e.SerialNumber), ct);
    }

    /// <summary>HU-014.</summary>
    public async Task HandleAsync(string eventId, DeviceUnlinkedEvent e, CancellationToken ct)
    {
        var now = Now();
        await devices.RemoveAsync(e.PlaceId, e.DeviceId, ct);
        await Notify(e.UserId, NotificationType.DeviceUnlinked, NotificationSeverity.Info, "Dispositivo desvinculado",
            $"El dispositivo {e.SerialNumber} dejó de estar vinculado a tu lugar.", e.PlaceId, eventId, now, Label(e.SerialNumber), ct);
    }

    /// <summary>
    /// HU-033: the valve closed (by anyone or anything) → CRITICAL. Only CHANGES notify: the device also
    /// reports periodically. Reports of a place without a known owner are ignored (nobody to tell).
    /// </summary>
    public async Task HandleAsync(string eventId, ValveReportedEvent e, CancellationToken ct)
    {
        var state = (e.State ?? "").Trim().ToUpperInvariant() switch
        {
            "OPEN" => ValveStatus.Open,
            "CLOSED" => ValveStatus.Closed,
            _ => throw new InvalidValveReportException(e.State),
        };

        var device = await devices.GetByPlaceAsync(e.PlaceId, ct);
        if (device is null || device.DeviceId != e.DeviceId) return;   // late report of an unlinked / replaced device

        var result = device.ApplyValveReport(state, DateTime.SpecifyKind(e.ReportedAt, DateTimeKind.Utc));
        if (!result.Applied) return;

        var now = Now();
        switch (result.Change)
        {
            case ValveChange.Closed:
                await Notify(device.UserId, NotificationType.ValveChanged, NotificationSeverity.Critical, "Válvula cerrada",
                    "El paso del agua de tu lugar se cerró y el lugar quedó sin agua. Si no fuiste tú, revisa el historial de la válvula y ábrela desde la app cuando sea seguro.",
                    e.PlaceId, eventId, now, Label(device.SerialNumber), ct);
                break;
            case ValveChange.Opened:
                await Notify(device.UserId, NotificationType.ValveChanged, NotificationSeverity.Info, "Válvula abierta",
                    "El paso del agua de tu lugar se restableció.", e.PlaceId, eventId, now, Label(device.SerialNumber), ct);
                break;
        }

        // Saved AFTER notifying: if we crash in between, the redelivered event finds the state unchanged,
        // notifies again (the source event id makes that a no-op if it already went out) and then saves.
        await devices.SaveValveReportAsync(device, ct);
    }

    private static string Label(string serial) => $"Lugar con el dispositivo {serial}";

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private Task Notify(Guid userId, NotificationType type, NotificationSeverity severity, string title, string body,
        Guid placeId, string eventId, DateTime now, string? placeLabel, CancellationToken ct) =>
        dispatcher.DispatchAsync(
            Notification.Create(Audience.ForUser(userId), type, severity, title, body, placeId, eventId, now),
            new DeliveryContext(placeLabel), ct);
}
