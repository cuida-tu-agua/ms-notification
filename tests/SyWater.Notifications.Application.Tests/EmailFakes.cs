using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Emails;

namespace SyWater.Notifications.Application.Tests;

public sealed class FakeOutbox : IEmailOutboxRepository
{
    public List<EmailOutboxItem> All { get; } = [];

    public Task<bool> AddAsync(EmailOutboxItem item, CancellationToken ct)
    {
        if (item.SourceEventId is not null && All.Any(i => i.SourceEventId == item.SourceEventId && i.UserId == item.UserId))
            return Task.FromResult(false);
        All.Add(item);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<EmailOutboxItem>> GetDueAsync(DateTime now, int max, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EmailOutboxItem>>(All.Where(i => i.IsDue(now)).Take(max).ToList());

    public Task<bool> SaveOutcomeAsync(EmailOutboxItem item, CancellationToken ct) => Task.FromResult(true);   // same instance in memory
}

public sealed class FakeContacts : IUserContactDirectory
{
    public Dictionary<Guid, UserContact> Known { get; } = [];
    public bool Down { get; set; }

    public Task<UserContact?> FindAsync(Guid userId, CancellationToken ct) =>
        Down ? throw new ExternalServiceUnavailableException("ms-iam") : Task.FromResult(Known.GetValueOrDefault(userId));
}

public sealed class FakeSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public bool Down { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (Down) throw new EmailSendException("SMTP down");
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
