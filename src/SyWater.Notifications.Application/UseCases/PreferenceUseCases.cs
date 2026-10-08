using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.UseCases;

public sealed class GetMyPreferencesUseCase(INotificationPreferenceRepository preferences) : IGetMyPreferencesUseCase
{
    public async Task<PreferencesView> ExecuteAsync(Requester requester, CancellationToken ct)
    {
        var mine = await preferences.GetAsync(requester.UserId, ct);
        return new PreferencesView(Enum.GetValues<NotificationSeverity>()
            .Select(level => LevelPreferenceView.From(level, mine.ChannelsFor(level))).ToList());
    }
}

public sealed class UpdateMyPreferencesUseCase(INotificationPreferenceRepository preferences, TimeProvider clock)
    : IUpdateMyPreferencesUseCase
{
    public async Task<LevelPreferenceView> ExecuteAsync(
        Requester requester, NotificationSeverity severity, bool inApp, bool push, bool email, bool sms, CancellationToken ct)
    {
        var wanted = new LevelPreferenceView(severity, inApp, push, email, sms).ToChannels();

        var mine = await preferences.GetAsync(requester.UserId, ct);
        mine.Set(severity, wanted);   // throws if a CRITICAL level would lose the in-app channel
        await preferences.SaveAsync(requester.UserId, severity, mine.ChannelsFor(severity), clock.GetUtcNow().UtcDateTime, ct);
        return LevelPreferenceView.From(severity, mine.ChannelsFor(severity));
    }
}
