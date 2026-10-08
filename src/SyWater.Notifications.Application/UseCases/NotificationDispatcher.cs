using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Notifications;
using SyWater.Notifications.Domain.Preferences;

namespace SyWater.Notifications.Application.UseCases;

/// <summary>
/// Where a freshly created notification goes, according to the channels its owner chose for its urgency
/// level (HU-034). The in-app notification is the one stored in the notification center; the other channels
/// are delivered by their own adapters.
/// </summary>
public sealed class NotificationDispatcher(INotificationRepository notifications, INotificationPreferenceRepository preferences)
{
    /// <summary>
    /// Stores it if the user keeps the in-app channel (CRITICAL always does) and returns the channels it must
    /// still be delivered through. A broadcast to a role is not personal: it is always stored in-app.
    /// </summary>
    public async Task<NotificationChannels> DispatchAsync(Notification notification, CancellationToken ct)
    {
        if (notification.Audience.UserId is not { } userId)
        {
            await notifications.AddAsync(notification, ct);
            return NotificationChannels.None;
        }

        var channels = (await preferences.GetAsync(userId, ct)).ChannelsFor(notification.Severity);
        if (channels.HasFlag(NotificationChannels.InApp) && !await notifications.AddAsync(notification, ct))
            return NotificationChannels.None;   // already created for this event: nothing to deliver twice

        return channels & ~NotificationChannels.InApp;
    }
}
