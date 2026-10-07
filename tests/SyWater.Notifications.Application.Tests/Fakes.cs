using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.Tests;

public sealed class FakeClock(DateTime start) : TimeProvider
{
    public DateTime Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
}

public sealed class FakeNotifications : INotificationRepository
{
    public List<Notification> All { get; } = [];
    public Dictionary<(Guid NotificationId, Guid UserId), DateTime> Reads { get; } = [];

    public Task<bool> AddAsync(Notification n, CancellationToken ct)
    {
        if (n.SourceEventId is not null && All.Any(x => x.SourceEventId == n.SourceEventId && x.Audience == n.Audience))
            return Task.FromResult(false);
        All.Add(n);
        return Task.FromResult(true);
    }

    public Task<Notification?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(All.FirstOrDefault(n => n.Id == id));

    private IEnumerable<NotificationWithRead> Visible(Guid userId, IReadOnlyCollection<string> roles) =>
        All.Where(n => n.Audience.Includes(userId, roles))
           .Select(n => new NotificationWithRead(n, Reads.TryGetValue((n.Id, userId), out var at) ? at : null));

    public Task<IReadOnlyList<NotificationWithRead>> ListForAsync(Guid userId, IReadOnlyCollection<string> roles,
        bool unreadOnly, DateTime? beforeUtc, int max, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NotificationWithRead>>(Visible(userId, roles)
            .Where(i => !unreadOnly || i.ReadAt is null)
            .Where(i => beforeUtc is null || i.Notification.CreatedAt < beforeUtc)
            .OrderByDescending(i => i.Notification.CreatedAt).Take(max).ToList());

    public Task<int> CountUnreadAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct) =>
        Task.FromResult(Visible(userId, roles).Count(i => i.ReadAt is null));

    public Task<DateTime> MarkReadAsync(Guid notificationId, Guid userId, DateTime now, CancellationToken ct)
    {
        Reads.TryAdd((notificationId, userId), now);
        return Task.FromResult(Reads[(notificationId, userId)]);
    }

    public Task<int> MarkAllReadAsync(Guid userId, IReadOnlyCollection<string> roles, DateTime now, CancellationToken ct)
    {
        var unread = Visible(userId, roles).Where(i => i.ReadAt is null).ToList();
        foreach (var i in unread) Reads[(i.Notification.Id, userId)] = now;
        return Task.FromResult(unread.Count);
    }
}
