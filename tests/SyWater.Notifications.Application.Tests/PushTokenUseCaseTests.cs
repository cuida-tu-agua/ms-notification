using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.UseCases;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Push;

namespace SyWater.Notifications.Application.Tests;

public sealed class FakePushTokens : IPushTokenRepository
{
    public List<PushToken> All { get; } = [];

    public Task UpsertAsync(Guid userId, string token, PushPlatform platform, DateTime now, CancellationToken ct)
    {
        var existing = All.FindIndex(t => t.Token == token);
        if (existing >= 0) All[existing] = All[existing] with { UserId = userId, Platform = platform, LastSeenAt = now };
        else All.Add(new PushToken(Guid.NewGuid(), userId, token, platform, now, now));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid userId, string token, CancellationToken ct)
    {
        All.RemoveAll(t => t.UserId == userId && t.Token == token);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PushToken>> ListForUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PushToken>>(All.Where(t => t.UserId == userId).ToList());

    public Task RemoveTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken ct)
    {
        All.RemoveAll(t => tokens.Contains(t.Token));
        return Task.CompletedTask;
    }
}

public class PushTokenUseCaseTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));
    private readonly FakePushTokens _tokens = new();
    private readonly Requester _me = new(Guid.NewGuid(), ["USER"]);
    private readonly Requester _other = new(Guid.NewGuid(), ["USER"]);
    private const string Phone = "ExponentPushToken[phone-1]";

    private RegisterPushTokenUseCase Register() => new(_tokens, _clock);
    private UnregisterPushTokenUseCase Unregister() => new(_tokens);

    [Fact]
    public async Task Registering_stores_the_token_of_the_user()
    {
        await Register().ExecuteAsync(_me, Phone, "android", default);

        var saved = Assert.Single(_tokens.All);
        Assert.Equal(_me.UserId, saved.UserId);
        Assert.Equal(PushPlatform.Android, saved.Platform);
    }

    [Fact]
    public async Task Registering_twice_keeps_one_row_and_refreshes_last_seen()
    {
        await Register().ExecuteAsync(_me, Phone, "android", default);
        _clock.Now = _clock.Now.AddHours(1);
        await Register().ExecuteAsync(_me, Phone, "android", default);

        Assert.Equal(_clock.Now, Assert.Single(_tokens.All).LastSeenAt);
    }

    [Fact]
    public async Task A_phone_where_another_user_signs_in_changes_owner()
    {
        await Register().ExecuteAsync(_me, Phone, "ios", default);
        await Register().ExecuteAsync(_other, Phone, "ios", default);

        Assert.Equal(_other.UserId, Assert.Single(_tokens.All).UserId);
    }

    [Fact]
    public async Task A_user_can_have_several_phones()
    {
        await Register().ExecuteAsync(_me, Phone, "android", default);
        await Register().ExecuteAsync(_me, "ExponentPushToken[phone-2]", "ios", default);

        Assert.Equal(2, (await _tokens.ListForUserAsync(_me.UserId, default)).Count);
    }

    [Fact]
    public async Task An_invalid_token_or_platform_stores_nothing()
    {
        await Assert.ThrowsAsync<InvalidPushTokenException>(() => Register().ExecuteAsync(_me, "nope", "android", default));
        await Assert.ThrowsAsync<InvalidPushPlatformException>(() => Register().ExecuteAsync(_me, Phone, "web", default));
        Assert.Empty(_tokens.All);
    }

    [Fact]
    public async Task Unregistering_removes_only_my_token_and_is_idempotent()
    {
        await Register().ExecuteAsync(_me, Phone, "android", default);

        await Unregister().ExecuteAsync(_other, Phone, default);   // not mine: nothing happens
        Assert.Single(_tokens.All);

        await Unregister().ExecuteAsync(_me, Phone, default);
        await Unregister().ExecuteAsync(_me, Phone, default);      // already gone: still fine
        Assert.Empty(_tokens.All);
    }
}
