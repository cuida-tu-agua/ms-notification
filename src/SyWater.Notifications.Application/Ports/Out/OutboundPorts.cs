using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.Ports.Out;

/// <summary>Persistence of the notifications and of who read them. Visibility = Audience.Includes(userId, roles).</summary>
public interface INotificationRepository
{
    /// <summary>
    /// Saves it. False if the same source event already created a notification for that audience
    /// (a RabbitMQ event delivered twice).
    /// </summary>
    Task<bool> AddAsync(Notification notification, CancellationToken ct);

    /// <summary>The notification by id, whoever it is for (the use case checks the audience).</summary>
    Task<Notification?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Notifications for this user (personal ones + the ones of their roles), newest first, at most
    /// <paramref name="max"/>, created before <paramref name="beforeUtc"/> when given.
    /// </summary>
    Task<IReadOnlyList<NotificationWithRead>> ListForAsync(Guid userId, IReadOnlyCollection<string> roles,
        bool unreadOnly, DateTime? beforeUtc, int max, CancellationToken ct);

    Task<int> CountUnreadAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);

    /// <summary>Idempotent: reading twice keeps the first date.</summary>
    Task<DateTime> MarkReadAsync(Guid notificationId, Guid userId, DateTime now, CancellationToken ct);

    /// <summary>Marks every unread notification of this user. Returns how many.</summary>
    Task<int> MarkAllReadAsync(Guid userId, IReadOnlyCollection<string> roles, DateTime now, CancellationToken ct);
}
