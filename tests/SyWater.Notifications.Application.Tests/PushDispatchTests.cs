using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.UseCases;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;
using SyWater.Notifications.Domain.Preferences;
using SyWater.Notifications.Domain.Push;

namespace SyWater.Notifications.Application.Tests;

public sealed class FakePushSender : IPushSender
{
    public List<PushMessage> Sent { get; } = [];
    public HashSet<string> DeadTokens { get; } = [];
    public bool Throws { get; set; }

    public Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        Sent.AddRange(messages);
        return Task.FromResult<IReadOnlyList<PushResult>>(messages
            .Select(m => new PushResult(m.Token, DeadTokens.Contains(m.Token) ? PushOutcome.DeviceNotRegistered : PushOutcome.Delivered)).ToList());
    }
}

/// <summary>A push notifier that is switched off, for the tests that are not about push.</summary>
public static class TestPush
{
    public static PushNotifier Off() => new(new FakePushTokens(), new FakePushSender(), new PushSettings(false));
}

public class PushDispatchTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));
    private readonly FakeNotifications _notifications = new();
    private readonly FakePreferences _prefs = new();
    private readonly FakePushTokens _tokens = new();
    private readonly FakePushSender _sender = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _place = Guid.NewGuid();

    private NotificationDispatcher Dispatcher(bool pushEnabled = true) => new(_notifications, _prefs, new FakeOutbox(),
        new EmailSettings(false, "http://app"), new PushNotifier(_tokens, _sender, new PushSettings(pushEnabled)), _clock);

    private Notification Personal(NotificationSeverity severity, string source = "evt-1") =>
        Notification.Create(Audience.ForUser(_me), NotificationType.ValveChanged, severity, "Válvula cerrada", "La válvula se cerró.",
            _place, source, _clock.Now);

    private async Task Phone(string token, Guid? user = null) =>
        await _tokens.UpsertAsync(user ?? _me, token, PushPlatform.Android, _clock.Now, default);

    [Fact]
    public async Task A_critical_alert_buzzes_every_phone_of_the_user_with_what_the_app_needs_to_open_it()
    {
        await Phone("ExponentPushToken[a]");
        await Phone("ExponentPushToken[b]");
        await Phone("ExponentPushToken[other]", Guid.NewGuid());   // somebody else's phone

        await Dispatcher().DispatchAsync(Personal(NotificationSeverity.Critical), new DeliveryContext("Casa"), default);

        Assert.Equal(["ExponentPushToken[a]", "ExponentPushToken[b]"], _sender.Sent.Select(m => m.Token).Order());
        var push = _sender.Sent[0];
        Assert.Equal("Válvula cerrada", push.Title);
        Assert.Equal(_notifications.All.Single().Id.ToString(), push.Data["notificationId"]);
        Assert.Equal(_place.ToString(), push.Data["placeId"]);
        Assert.Equal("ValveChanged", push.Data["type"]);
    }

    [Fact]
    public async Task The_push_channel_of_the_level_decides_not_the_mere_existence_of_a_phone()
    {
        await Phone("ExponentPushToken[a]");
        await new UpdateMyPreferencesUseCase(_prefs, _clock)
            .ExecuteAsync(new Requester(_me, ["USER"]), NotificationSeverity.Critical, true, false, true, false, default);   // push off

        await Dispatcher().DispatchAsync(Personal(NotificationSeverity.Critical), new DeliveryContext("Casa"), default);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Info_notices_do_not_push_by_default()
    {
        await Phone("ExponentPushToken[a]");

        await Dispatcher().DispatchAsync(Personal(NotificationSeverity.Info), new DeliveryContext("Casa"), default);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task A_redelivered_event_does_not_buzz_the_phone_twice()
    {
        await Phone("ExponentPushToken[a]");

        await Dispatcher().DispatchAsync(Personal(NotificationSeverity.Critical, "evt-9"), new DeliveryContext("Casa"), default);
        await Dispatcher().DispatchAsync(Personal(NotificationSeverity.Critical, "evt-9"), new DeliveryContext("Casa"), default);

        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task A_user_without_phones_gets_no_push_and_no_error()
    {
        await Dispatcher().DispatchAsync(Personal(NotificationSeverity.Critical), new DeliveryContext("Casa"), default);

        Assert.Empty(_sender.Sent);
        Assert.Single(_notifications.All);   // the in-app notification is there anyway
    }

    [Fact]
    public async Task Phones_that_expo_says_are_gone_are_forgotten()
    {
        await Phone("ExponentPushToken[alive]");
        await Phone("ExponentPushToken[dead]");
        _sender.DeadTokens.Add("ExponentPushToken[dead]");

        await Dispatcher().DispatchAsync(Personal(NotificationSeverity.Critical), new DeliveryContext("Casa"), default);

        Assert.Equal("ExponentPushToken[alive]", Assert.Single(await _tokens.ListForUserAsync(_me, default)).Token);
    }

    [Fact]
    public async Task With_the_push_channel_switched_off_nothing_is_sent_and_the_dispatcher_reports_it()
    {
        await Phone("ExponentPushToken[a]");

        var notDelivered = await Dispatcher(pushEnabled: false).DispatchAsync(Personal(NotificationSeverity.Critical), new DeliveryContext("Casa"), default);

        Assert.Empty(_sender.Sent);
        Assert.True(notDelivered.HasFlag(NotificationChannels.Push));
    }

    [Fact]
    public async Task A_role_broadcast_is_never_pushed()
    {
        await Phone("ExponentPushToken[a]");
        var broadcast = Notification.Create(Audience.ForRole(Roles.Admin), NotificationType.SystemAnnouncement, NotificationSeverity.Critical,
            "Aviso", "Mantenimiento", null, "evt-b", _clock.Now);

        await Dispatcher().DispatchAsync(broadcast, new DeliveryContext(), default);

        Assert.Empty(_sender.Sent);
    }
}
