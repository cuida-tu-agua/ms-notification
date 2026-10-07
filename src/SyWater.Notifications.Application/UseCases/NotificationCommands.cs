using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.UseCases;

/// <summary>
/// Someone else's notification (or one of a role you do not have) → 404, like one that does not exist:
/// nobody can find out which ids are real.
/// </summary>
public sealed class MarkNotificationReadUseCase(INotificationRepository notifications, TimeProvider clock)
    : IMarkNotificationReadUseCase
{
    public async Task<NotificationView> ExecuteAsync(Requester requester, Guid notificationId, CancellationToken ct)
    {
        var notification = await notifications.GetAsync(notificationId, ct);
        if (notification is null || !notification.Audience.Includes(requester.UserId, requester.Roles))
            throw new NotificationNotFoundException(notificationId);

        var now = clock.GetUtcNow().UtcDateTime;
        var readAt = await notifications.MarkReadAsync(notificationId, requester.UserId, now, ct);
        return NotificationView.From(new NotificationWithRead(notification, readAt));
    }
}

public sealed class MarkAllNotificationsReadUseCase(INotificationRepository notifications, TimeProvider clock)
    : IMarkAllNotificationsReadUseCase
{
    public Task<int> ExecuteAsync(Requester requester, CancellationToken ct) =>
        notifications.MarkAllReadAsync(requester.UserId, requester.Roles, clock.GetUtcNow().UtcDateTime, ct);
}
