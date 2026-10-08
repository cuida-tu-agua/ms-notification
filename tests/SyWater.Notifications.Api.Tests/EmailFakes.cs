using SyWater.Notifications.Application.Ports.Out;

namespace SyWater.Notifications.Api.Tests;

public sealed class FakeContacts : IUserContactDirectory
{
    public Dictionary<Guid, UserContact> Known { get; } = [];
    public Task<UserContact?> FindAsync(Guid userId, CancellationToken ct) => Task.FromResult(Known.GetValueOrDefault(userId));
}

public sealed class FakeSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        lock (Sent) Sent.Add(message);
        return Task.CompletedTask;
    }
}
