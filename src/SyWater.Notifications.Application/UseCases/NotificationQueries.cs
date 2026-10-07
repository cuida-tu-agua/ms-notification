using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.Views;

namespace SyWater.Notifications.Application.UseCases;

public sealed class ListMyNotificationsUseCase(INotificationRepository notifications) : IListMyNotificationsUseCase
{
    public const int DefaultLimit = 30;
    public const int MaxLimit = 100;

    public async Task<IReadOnlyList<NotificationView>> ExecuteAsync(
        Requester requester, bool unreadOnly, DateTime? beforeUtc, int? limit, CancellationToken ct)
    {
        var max = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var items = await notifications.ListForAsync(requester.UserId, requester.Roles, unreadOnly, beforeUtc, max, ct);
        return items.Select(NotificationView.From).ToList();
    }
}

public sealed class GetUnreadCountUseCase(INotificationRepository notifications) : IGetUnreadCountUseCase
{
    public async Task<UnreadCountView> ExecuteAsync(Requester requester, CancellationToken ct) =>
        new(await notifications.CountUnreadAsync(requester.UserId, requester.Roles, ct));
}
