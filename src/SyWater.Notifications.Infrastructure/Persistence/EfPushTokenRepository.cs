using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Push;
using SyWater.Notifications.Infrastructure.Persistence.Entities;

namespace SyWater.Notifications.Infrastructure.Persistence;

public sealed class EfPushTokenRepository(NotificationDbContext db) : IPushTokenRepository
{
    public async Task UpsertAsync(Guid userId, string token, PushPlatform platform, DateTime now, CancellationToken ct)
    {
        var platformDb = PushToken.PlatformToDb(platform);
        try
        {
            var row = await db.PushTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
            if (row is null)
            {
                row = new PushTokenEntity { Id = Guid.NewGuid(), Token = token, CreatedAt = now };
                db.PushTokens.Add(row);
            }
            row.UserId = userId;
            row.Platform = platformDb;
            row.LastSeenAt = now;
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // UQ_ptok_token: the same phone registered twice at the same time → update the winner's row
            db.ChangeTracker.Clear();
            await db.PushTokens.Where(t => t.Token == token).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.UserId, userId)
                .SetProperty(t => t.Platform, platformDb)
                .SetProperty(t => t.LastSeenAt, now), ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public Task RemoveAsync(Guid userId, string token, CancellationToken ct) =>
        db.PushTokens.Where(t => t.UserId == userId && t.Token == token).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<PushToken>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.PushTokens.AsNoTracking().Where(t => t.UserId == userId).OrderBy(t => t.CreatedAt).ToListAsync(ct);
        return rows.Select(r => new PushToken(r.Id, r.UserId, r.Token,
            r.Platform == "IOS" ? PushPlatform.Ios : PushPlatform.Android, r.CreatedAt, r.LastSeenAt)).ToList();
    }

    public Task RemoveTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken ct) =>
        tokens.Count == 0 ? Task.CompletedTask : db.PushTokens.Where(t => tokens.Contains(t.Token)).ExecuteDeleteAsync(ct);
}
