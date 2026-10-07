using SyWater.Notifications.Application.Events;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.UseCases;
using SyWater.Notifications.Domain.Devices;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.Tests;

public sealed class FakePlaceDevices : IPlaceDeviceRepository
{
    public Dictionary<Guid, PlaceDevice> ByPlace { get; } = [];
    public Task<PlaceDevice?> GetByPlaceAsync(Guid placeId, CancellationToken ct) => Task.FromResult(ByPlace.GetValueOrDefault(placeId));
    public Task ReplaceAsync(PlaceDevice device, DateTime now, CancellationToken ct)
    {
        foreach (var key in ByPlace.Where(kv => kv.Value.DeviceId == device.DeviceId).Select(kv => kv.Key).ToList()) ByPlace.Remove(key);
        ByPlace[device.PlaceId] = device;
        return Task.CompletedTask;
    }
    public Task RemoveAsync(Guid placeId, Guid deviceId, CancellationToken ct)
    {
        if (ByPlace.TryGetValue(placeId, out var d) && d.DeviceId == deviceId) ByPlace.Remove(placeId);
        return Task.CompletedTask;
    }
    public Task SaveValveReportAsync(PlaceDevice device, CancellationToken ct) => Task.CompletedTask;   // same instance in memory
}

public class DeviceEventsHandlerTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
    private readonly FakePlaceDevices _devices = new();
    private readonly FakeNotifications _notifications = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _place = Guid.NewGuid();
    private readonly Guid _device = Guid.NewGuid();

    private DeviceEventsHandler Handler() => new(_devices, _notifications, _clock);

    private Task Link(string eventId = "evt-link") =>
        Handler().HandleAsync(eventId, new DeviceLinkedEvent(_device, "SW-ESP32-000001", _place, _user, _clock.Now), default);

    private Task Report(string state, string eventId, int minutes) =>
        Handler().HandleAsync(eventId, new ValveReportedEvent(_device, _place, state, null, _clock.Now.AddMinutes(minutes)), default);

    [Fact]
    public async Task Linking_tells_the_owner_and_remembers_who_owns_the_place()
    {
        await Link();

        var n = Assert.Single(_notifications.All);
        Assert.Equal(_user, n.Audience.UserId);
        Assert.Equal(NotificationType.DeviceLinked, n.Type);
        Assert.Equal(_place, n.PlaceId);
        Assert.Equal(_user, _devices.ByPlace[_place].UserId);
    }

    [Fact]
    public async Task A_redelivered_event_creates_one_notification()
    {
        await Link("evt-1");
        await Link("evt-1");

        Assert.Single(_notifications.All);
    }

    [Fact]
    public async Task Unlinking_tells_the_owner_and_forgets_the_place()
    {
        await Link();
        await Handler().HandleAsync("evt-unlink", new DeviceUnlinkedEvent(_device, "SW-ESP32-000001", _place, _user, _clock.Now), default);

        Assert.Equal(NotificationType.DeviceUnlinked, _notifications.All[^1].Type);
        Assert.Empty(_devices.ByPlace);
    }

    [Fact]
    public async Task A_valve_that_closes_is_a_critical_notification_for_the_owner_only_once()
    {
        await Link();
        await Report("CLOSED", "evt-r1", 1);
        await Report("CLOSED", "evt-r2", 2);   // periodic report, same state

        var critical = Assert.Single(_notifications.All, n => n.Severity == NotificationSeverity.Critical);
        Assert.Equal(NotificationType.ValveChanged, critical.Type);
        Assert.Equal(_user, critical.Audience.UserId);
        Assert.Equal("evt-r1", critical.SourceEventId);
    }

    [Fact]
    public async Task Reopening_is_informative_and_a_normal_open_report_is_silent()
    {
        await Link();
        await Report("OPEN", "evt-r1", 1);
        Assert.Single(_notifications.All);   // only the link one

        await Report("CLOSED", "evt-r2", 2);
        await Report("OPEN", "evt-r3", 3);

        var last = _notifications.All[^1];
        Assert.Equal(NotificationSeverity.Info, last.Severity);
        Assert.Equal("Válvula abierta", last.Title);
    }

    [Fact]
    public async Task Reports_of_unknown_or_replaced_devices_notify_nobody()
    {
        await Report("CLOSED", "evt-r1", 1);   // place with no known owner
        await Link();
        await Handler().HandleAsync("evt-r2", new ValveReportedEvent(Guid.NewGuid(), _place, "CLOSED", null, _clock.Now.AddMinutes(1)), default);

        Assert.DoesNotContain(_notifications.All, n => n.Severity == NotificationSeverity.Critical);
    }

    [Fact]
    public async Task A_report_with_an_invalid_state_is_rejected()
    {
        await Link();

        await Assert.ThrowsAsync<InvalidValveReportException>(() => Report("HALF", "evt-r1", 1));
    }
}
