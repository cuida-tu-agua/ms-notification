using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Emails;
using SyWater.Notifications.Domain.Notifications;
using SyWater.Notifications.Domain.Preferences;

namespace SyWater.Notifications.Application.UseCases;

/// <summary>
/// Where a freshly created notification goes, according to the channels its owner chose for its urgency
/// level (HU-034). In-app = the notification center; e-mail = queued in the outbox (HU-027); push = Expo, sent now
/// to every phone of the user (HU-026).
/// Every step is idempotent, so a redelivered event can safely run the whole thing again.
/// </summary>
public sealed class NotificationDispatcher(
    INotificationRepository notifications,
    INotificationPreferenceRepository preferences,
    IEmailOutboxRepository outbox,
    EmailSettings email,
    PushNotifier push,
    TimeProvider clock)
{
    /// <summary>
    /// Stores it in-app if the user keeps that channel (CRITICAL always does) and queues the mail if they want
    /// it. A broadcast to a role is not personal: it is always stored in-app and never mailed.
    /// </summary>
    /// <returns>The channels the user wants that this service did not deliver (SMS, and push while it is switched off).</returns>
    public async Task<NotificationChannels> DispatchAsync(Notification notification, DeliveryContext context, CancellationToken ct)
    {
        if (notification.Audience.UserId is not { } userId)
        {
            await notifications.AddAsync(notification, ct);
            return NotificationChannels.None;
        }

        var channels = (await preferences.GetAsync(userId, ct)).ChannelsFor(notification.Severity);

        var isNew = true;
        if (channels.HasFlag(NotificationChannels.InApp))
            isNew = await notifications.AddAsync(notification, ct);

        if (channels.HasFlag(NotificationChannels.Email) && email.Enabled)
        {
            var (subject, text, html) = EmailComposer.Compose(notification, context, email.AppBaseUrl);
            await outbox.AddAsync(
                EmailOutboxItem.Queue(userId, notification.SourceEventId, subject, text, html, clock.GetUtcNow().UtcDateTime), ct);
        }

        // A redelivered event (already stored in-app) must not buzz the phone twice
        if (channels.HasFlag(NotificationChannels.Push) && isNew)
            await push.NotifyAsync(userId, notification, ct);

        var notDelivered = NotificationChannels.Sms | (push.Enabled ? NotificationChannels.None : NotificationChannels.Push);
        return channels & notDelivered;
    }
}
