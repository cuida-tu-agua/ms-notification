using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Notifications;
using SyWater.Notifications.Domain.Preferences;
using SyWater.Notifications.Infrastructure.Persistence.Entities;

namespace SyWater.Notifications.Infrastructure.Persistence;

public sealed class EfNotificationPreferenceRepository(NotificationDbContext db) : INotificationPreferenceRepository
{
    public async Task<ChannelPreferences> GetAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.Preferences.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(ct);
        return ChannelPreferences.Restore(rows.ToDictionary(r => SeverityFromDb(r.Severity), ToChannels));
    }

    public async Task SaveAsync(Guid userId, NotificationSeverity severity, NotificationChannels channels, DateTime now, CancellationToken ct)
    {
        var key = NotificationMapper.ToDb(severity);
        try
        {
            var row = await db.Preferences.FirstOrDefaultAsync(p => p.UserId == userId && p.Severity == key, ct);
            if (row is null)
            {
                row = new NotificationPreferenceEntity { UserId = userId, Severity = key };
                db.Preferences.Add(row);
            }
            Apply(row, channels, now);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // PK_notification_preferences: two phones saved the same level at the same time → update the winner's row
            db.ChangeTracker.Clear();
            await db.Preferences.Where(p => p.UserId == userId && p.Severity == key).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.InApp, channels.HasFlag(NotificationChannels.InApp))
                .SetProperty(p => p.Push, channels.HasFlag(NotificationChannels.Push))
                .SetProperty(p => p.Email, channels.HasFlag(NotificationChannels.Email))
                .SetProperty(p => p.Sms, channels.HasFlag(NotificationChannels.Sms))
                .SetProperty(p => p.UpdatedAt, now), ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private static void Apply(NotificationPreferenceEntity row, NotificationChannels c, DateTime now)
    {
        row.InApp = c.HasFlag(NotificationChannels.InApp);
        row.Push = c.HasFlag(NotificationChannels.Push);
        row.Email = c.HasFlag(NotificationChannels.Email);
        row.Sms = c.HasFlag(NotificationChannels.Sms);
        row.UpdatedAt = now;
    }

    private static NotificationChannels ToChannels(NotificationPreferenceEntity r) =>
        (r.InApp ? NotificationChannels.InApp : 0) | (r.Push ? NotificationChannels.Push : 0)
        | (r.Email ? NotificationChannels.Email : 0) | (r.Sms ? NotificationChannels.Sms : 0);

    private static NotificationSeverity SeverityFromDb(string s) => s switch
    {
        "CRITICAL" => NotificationSeverity.Critical,
        "WARNING" => NotificationSeverity.Warning,
        _ => NotificationSeverity.Info,
    };
}
