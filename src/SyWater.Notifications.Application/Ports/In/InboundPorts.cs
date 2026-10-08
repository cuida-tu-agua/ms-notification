using SyWater.Notifications.Application.Views;

namespace SyWater.Notifications.Application.Ports.In;

/// <summary>My inbox: personal notifications + the ones sent to my roles.</summary>
public interface IListMyNotificationsUseCase
{
    Task<IReadOnlyList<NotificationView>> ExecuteAsync(Requester requester, bool unreadOnly, DateTime? beforeUtc, int? limit, CancellationToken ct);
}

/// <summary>The number on the bell icon.</summary>
public interface IGetUnreadCountUseCase
{
    Task<UnreadCountView> ExecuteAsync(Requester requester, CancellationToken ct);
}

public interface IMarkNotificationReadUseCase
{
    Task<NotificationView> ExecuteAsync(Requester requester, Guid notificationId, CancellationToken ct);
}

public interface IMarkAllNotificationsReadUseCase
{
    /// <returns>How many notifications were marked.</returns>
    Task<int> ExecuteAsync(Requester requester, CancellationToken ct);
}

/// <summary>HU-027: sends the mails of the outbox that are due. Returns how many left.</summary>
public interface ISendDueEmailsUseCase
{
    Task<int> ExecuteAsync(CancellationToken ct);
}
