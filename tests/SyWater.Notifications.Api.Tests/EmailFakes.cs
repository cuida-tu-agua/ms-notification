using SyWater.Notifications.Application.Ports.Out;

namespace SyWater.Notifications.Api.Tests;

public sealed class FakeContacts : IUserContactDirectory
{
    public Dictionary<Guid, UserContact> Known { get; } = [];
    public Task<UserContact?> FindAsync(Guid userId, CancellationToken ct) => Task.FromResult(Known.GetValueOrDefault(userId));
}

/// <summary>Records the pushes instead of calling Expo.</summary>
public sealed class FakePushSender : IPushSender
{
    public List<PushMessage> Sent { get; } = [];
    public Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        lock (Sent) Sent.AddRange(messages);
        return Task.FromResult<IReadOnlyList<PushResult>>(messages.Select(m => new PushResult(m.Token, PushOutcome.Delivered)).ToList());
    }
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
