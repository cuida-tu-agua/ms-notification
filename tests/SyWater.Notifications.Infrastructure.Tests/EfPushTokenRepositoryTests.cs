using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Domain.Push;
using SyWater.Notifications.Infrastructure.Persistence;

namespace SyWater.Notifications.Infrastructure.Tests;

/// <summary>The real EF queries of the push tokens against SQLite in memory.</summary>
public sealed class EfPushTokenRepositoryTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private readonly SqliteConnection _sqlite = new("DataSource=:memory:");
    private readonly DbContextOptions<NotificationDbContext> _options;
    private readonly Guid _juan = Guid.NewGuid();
    private readonly Guid _ana = Guid.NewGuid();

    public EfPushTokenRepositoryTests()
    {
        _sqlite.Open();
        _options = new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite(_sqlite).Options;
        using var db = new NotificationDbContext(_options);
        db.Database.EnsureCreated();
    }

    private EfPushTokenRepository Repo() => new(new NotificationDbContext(_options));

    [Fact]
    public async Task A_token_survives_the_round_trip()
    {
        await Repo().UpsertAsync(_juan, "ExponentPushToken[a]", PushPlatform.Ios, Now, default);

        var saved = Assert.Single(await Repo().ListForUserAsync(_juan, default));
        Assert.Equal("ExponentPushToken[a]", saved.Token);
        Assert.Equal(PushPlatform.Ios, saved.Platform);
        Assert.Equal(Now, saved.LastSeenAt);
    }

    [Fact]
    public async Task Registering_the_same_token_again_updates_it_and_another_user_takes_it_over()
    {
        await Repo().UpsertAsync(_juan, "ExponentPushToken[a]", PushPlatform.Android, Now, default);
        await Repo().UpsertAsync(_juan, "ExponentPushToken[a]", PushPlatform.Android, Now.AddHours(1), default);
        Assert.Equal(Now.AddHours(1), Assert.Single(await Repo().ListForUserAsync(_juan, default)).LastSeenAt);

        await Repo().UpsertAsync(_ana, "ExponentPushToken[a]", PushPlatform.Android, Now.AddHours(2), default);

        Assert.Empty(await Repo().ListForUserAsync(_juan, default));
        Assert.Single(await Repo().ListForUserAsync(_ana, default));
    }

    [Fact]
    public async Task Remove_only_deletes_the_token_of_that_user()
    {
        await Repo().UpsertAsync(_juan, "ExponentPushToken[a]", PushPlatform.Android, Now, default);

        await Repo().RemoveAsync(_ana, "ExponentPushToken[a]", default);
        Assert.Single(await Repo().ListForUserAsync(_juan, default));

        await Repo().RemoveAsync(_juan, "ExponentPushToken[a]", default);
        Assert.Empty(await Repo().ListForUserAsync(_juan, default));
    }

    [Fact]
    public async Task Dead_tokens_are_forgotten_whoever_owns_them()
    {
        await Repo().UpsertAsync(_juan, "ExponentPushToken[a]", PushPlatform.Android, Now, default);
        await Repo().UpsertAsync(_ana, "ExponentPushToken[b]", PushPlatform.Ios, Now, default);

        await Repo().RemoveTokensAsync(["ExponentPushToken[a]"], default);
        await Repo().RemoveTokensAsync([], default);

        Assert.Empty(await Repo().ListForUserAsync(_juan, default));
        Assert.Single(await Repo().ListForUserAsync(_ana, default));
    }

    public void Dispose() => _sqlite.Dispose();
}
