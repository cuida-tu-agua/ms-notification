using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;
using SyWater.Notifications.Infrastructure.Persistence.Entities;

namespace SyWater.Notifications.Infrastructure.Persistence;

public sealed class EfNotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task<bool> AddAsync(Notification notification, CancellationToken ct)
    {
        var row = NotificationMapper.ToEntity(notification);
        if (await ExistsForSourceAsync(row, ct)) return false;

        db.Notifications.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            // UX_notif_source_audience: the same event was processed a moment ago by another instance
            if (await ExistsForSourceAsync(row, ct)) return false;
            throw;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private Task<bool> ExistsForSourceAsync(NotificationEntity row, CancellationToken ct) =>
        row.SourceEventId is null
            ? Task.FromResult(false)
            : db.Notifications.AnyAsync(n => n.SourceEventId == row.SourceEventId
                                             && n.UserId == row.UserId && n.Role == row.Role, ct);

    public async Task<Notification?> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        return row is null ? null : NotificationMapper.ToDomain(row);
    }

    public async Task<IReadOnlyList<NotificationWithRead>> ListForAsync(Guid userId, IReadOnlyCollection<string> roles,
        bool unreadOnly, DateTime? beforeUtc, int max, CancellationToken ct)
    {
        var query = VisibleWithRead(userId, roles);
        if (unreadOnly) query = query.Where(x => x.ReadAt == null);
        if (beforeUtc is { } before) query = query.Where(x => x.Notification.CreatedAt < before);

        var rows = await query.OrderByDescending(x => x.Notification.CreatedAt).ThenByDescending(x => x.Notification.Id)
            .Take(max).ToListAsync(ct);
        return rows.Select(x => new NotificationWithRead(
            NotificationMapper.ToDomain(x.Notification), x.ReadAt is null ? null : NotificationMapper.Utc(x.ReadAt.Value))).ToList();
    }

    public Task<int> CountUnreadAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct) =>
        VisibleWithRead(userId, roles).CountAsync(x => x.ReadAt == null, ct);

    public async Task<DateTime> MarkReadAsync(Guid notificationId, Guid userId, DateTime now, CancellationToken ct)
    {
        var existing = await FindReadAsync(notificationId, userId, ct);
        if (existing is not null) return existing.Value;

        db.Reads.Add(new NotificationReadEntity { NotificationId = notificationId, UserId = userId, ReadAt = now });
        try
        {
            await db.SaveChangesAsync(ct);
            return now;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            // PK_notification_reads: another request of the same user (two phones) read it a moment ago
            return await FindReadAsync(notificationId, userId, ct) ?? throw new InvalidOperationException(
                $"Could not mark notification {notificationId} as read.");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<DateTime?> FindReadAsync(Guid notificationId, Guid userId, CancellationToken ct)
    {
        var at = await db.Reads.AsNoTracking()
            .Where(r => r.NotificationId == notificationId && r.UserId == userId)
            .Select(r => (DateTime?)r.ReadAt).FirstOrDefaultAsync(ct);
        return at is null ? null : NotificationMapper.Utc(at.Value);
    }

    public async Task<int> MarkAllReadAsync(Guid userId, IReadOnlyCollection<string> roles, DateTime now, CancellationToken ct)
    {
        var unreadIds = await VisibleWithRead(userId, roles).Where(x => x.ReadAt == null)
            .Select(x => x.Notification.Id).ToListAsync(ct);
        if (unreadIds.Count == 0) return 0;

        try
        {
            db.Reads.AddRange(unreadIds.Select(id => new NotificationReadEntity { NotificationId = id, UserId = userId, ReadAt = now }));
            await db.SaveChangesAsync(ct);
            return unreadIds.Count;
        }
        catch (DbUpdateException)
        {
            // Another request read some of them meanwhile: one retry with what is still unread
            db.ChangeTracker.Clear();
            var stillUnread = await VisibleWithRead(userId, roles).Where(x => x.ReadAt == null)
                .Select(x => x.Notification.Id).ToListAsync(ct);
            db.Reads.AddRange(stillUnread.Select(id => new NotificationReadEntity { NotificationId = id, UserId = userId, ReadAt = now }));
            await db.SaveChangesAsync(ct);
            return stillUnread.Count;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// THE filter by user and role: personal notifications (user_id = me) + broadcasts to one of my roles,
    /// each with MY read date (left join). Same rule as Audience.Includes in the domain.
    /// </summary>
    private IQueryable<VisibleRow> VisibleWithRead(Guid userId, IReadOnlyCollection<string> roles)
    {
        var myRoles = roles.Select(r => r.Trim().ToUpperInvariant()).Distinct().ToArray();   // stored upper-case
        return from n in db.Notifications.AsNoTracking()
               where n.UserId == userId || (n.Role != null && myRoles.Contains(n.Role))
               join r in db.Reads.Where(r => r.UserId == userId) on n.Id equals r.NotificationId into mine
               from read in mine.DefaultIfEmpty()
               select new VisibleRow { Notification = n, ReadAt = read == null ? null : read.ReadAt };
    }

    private sealed class VisibleRow
    {
        public NotificationEntity Notification { get; init; } = null!;
        public DateTime? ReadAt { get; init; }
    }
}
