using SyWater.Notifications.Domain.Notifications;
using SyWater.Notifications.Domain.Preferences;

namespace SyWater.Notifications.Application.Views;

/// <summary>The four switches of one urgency level.</summary>
public sealed record LevelPreferenceView(NotificationSeverity Severity, bool InApp, bool Push, bool Email, bool Sms)
{
    public static LevelPreferenceView From(NotificationSeverity severity, NotificationChannels c) => new(
        severity,
        c.HasFlag(NotificationChannels.InApp), c.HasFlag(NotificationChannels.Push),
        c.HasFlag(NotificationChannels.Email), c.HasFlag(NotificationChannels.Sms));

    public NotificationChannels ToChannels() =>
        (InApp ? NotificationChannels.InApp : 0) | (Push ? NotificationChannels.Push : 0)
        | (Email ? NotificationChannels.Email : 0) | (Sms ? NotificationChannels.Sms : 0);
}

/// <summary>One entry per level: Info, Warning, Critical.</summary>
public sealed record PreferencesView(IReadOnlyList<LevelPreferenceView> Levels);
