using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Emails;
using SyWater.Notifications.Infrastructure.Persistence.Entities;

namespace SyWater.Notifications.Infrastructure.Persistence;

public sealed class EfEmailOutboxRepository(NotificationDbContext db) : IEmailOutboxRepository
{
    public async Task<bool> AddAsync(EmailOutboxItem item, CancellationToken ct)
    {
        if (await ExistsAsync(item, ct)) return false;

        db.EmailOutbox.Add(new EmailOutboxEntity
        {
            Id = item.Id,
            UserId = item.UserId,
            SourceEventId = item.SourceEventId,
            Subject = item.Subject,
            TextBody = item.TextBody,
            HtmlBody = item.HtmlBody,
            Status = ToDb(item.Status),
            Attempts = item.Attempts,
            NextAttemptAt = item.NextAttemptAt,
            LastError = item.LastError,
            CreatedAt = item.CreatedAt,
            SentAt = item.SentAt,
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            // UX_eout_source_user: the same event was processed a moment ago by another instance
            if (await ExistsAsync(item, ct)) return false;
            throw;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private Task<bool> ExistsAsync(EmailOutboxItem item, CancellationToken ct) =>
        item.SourceEventId is null
            ? Task.FromResult(false)
            : db.EmailOutbox.AnyAsync(e => e.SourceEventId == item.SourceEventId && e.UserId == item.UserId, ct);

    public async Task<IReadOnlyList<EmailOutboxItem>> GetDueAsync(DateTime now, int max, CancellationToken ct)
    {
        var rows = await db.EmailOutbox.AsNoTracking()
            .Where(e => e.Status == "PENDING" && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt).Take(max).ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<bool> SaveOutcomeAsync(EmailOutboxItem item, CancellationToken ct) =>
        await db.EmailOutbox.Where(e => e.Id == item.Id && e.Status == "PENDING")
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, ToDb(item.Status))
                .SetProperty(e => e.Attempts, item.Attempts)
                .SetProperty(e => e.NextAttemptAt, item.NextAttemptAt)
                .SetProperty(e => e.LastError, item.LastError)
                .SetProperty(e => e.SentAt, item.SentAt), ct) > 0;

    private static EmailOutboxItem ToDomain(EmailOutboxEntity e) => EmailOutboxItem.Restore(
        e.Id, e.UserId, e.SourceEventId, e.Subject, e.TextBody, e.HtmlBody, StatusFromDb(e.Status), e.Attempts,
        NotificationMapper.Utc(e.NextAttemptAt), e.LastError, NotificationMapper.Utc(e.CreatedAt),
        e.SentAt is null ? null : NotificationMapper.Utc(e.SentAt.Value));

    private static string ToDb(EmailStatus s) => s switch
    {
        EmailStatus.Sent => "SENT",
        EmailStatus.Failed => "FAILED",
        _ => "PENDING",
    };

    private static EmailStatus StatusFromDb(string s) => s switch
    {
        "SENT" => EmailStatus.Sent,
        "FAILED" => EmailStatus.Failed,
        _ => EmailStatus.Pending,
    };
}
