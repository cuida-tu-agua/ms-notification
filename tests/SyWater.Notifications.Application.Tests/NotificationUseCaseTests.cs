using SyWater.Notifications.Application.UseCases;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.Tests;

public class NotificationUseCaseTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
    private readonly FakeNotifications _repo = new();
    private readonly Requester _juan = new(Guid.NewGuid(), [Roles.User]);
    private readonly Requester _ana = new(Guid.NewGuid(), [Roles.User]);
    private readonly Requester _admin = new(Guid.NewGuid(), [Roles.User, Roles.Admin]);

    private async Task<Notification> Add(Audience audience, int minutesAgo = 0, string? source = null)
    {
        var n = Notification.Create(audience, NotificationType.ValveChanged, NotificationSeverity.Info, "Title", "Body",
            null, source, _clock.Now.AddMinutes(-minutesAgo));
        await _repo.AddAsync(n, default);
        return n;
    }

    private ListMyNotificationsUseCase List() => new(_repo);

    [Fact]
    public async Task Each_user_sees_only_their_own_and_their_roles_notifications()
    {
        await Add(Audience.ForUser(_juan.UserId));
        await Add(Audience.ForUser(_ana.UserId));
        await Add(Audience.ForRole(Roles.Admin));
        await Add(Audience.ForRole(Roles.User));

        Assert.Equal(2, (await List().ExecuteAsync(_juan, false, null, null, default)).Count);   // personal + USER
        Assert.Equal(2, (await List().ExecuteAsync(_ana, false, null, null, default)).Count);
        Assert.Equal(2, (await List().ExecuteAsync(_admin, false, null, null, default)).Count);  // ADMIN + USER, no personal
    }

    [Fact]
    public async Task The_inbox_is_newest_first_paged_and_the_limit_is_capped()
    {
        for (var i = 0; i < 5; i++) await Add(Audience.ForUser(_juan.UserId), minutesAgo: i);

        var page = await List().ExecuteAsync(_juan, false, null, 2, default);
        Assert.Equal(2, page.Count);
        Assert.True(page[0].CreatedAt > page[1].CreatedAt);

        var next = await List().ExecuteAsync(_juan, false, page[^1].CreatedAt, 10, default);
        Assert.Equal(3, next.Count);
        Assert.All(next, n => Assert.True(n.CreatedAt < page[^1].CreatedAt));

        for (var i = 0; i < 150; i++) await Add(Audience.ForUser(_juan.UserId));
        Assert.Equal(ListMyNotificationsUseCase.MaxLimit, (await List().ExecuteAsync(_juan, false, null, 1000, default)).Count);
    }

    [Fact]
    public async Task Reading_a_role_notification_is_personal()
    {
        var n = await Add(Audience.ForRole(Roles.User));

        var view = await new MarkNotificationReadUseCase(_repo, _clock).ExecuteAsync(_juan, n.Id, default);

        Assert.True(view.IsRead);
        Assert.Equal(0, (await new GetUnreadCountUseCase(_repo).ExecuteAsync(_juan, default)).Count);
        Assert.Equal(1, (await new GetUnreadCountUseCase(_repo).ExecuteAsync(_ana, default)).Count);   // Ana still has it unread
    }

    [Fact]
    public async Task Marking_twice_keeps_the_first_read_date()
    {
        var n = await Add(Audience.ForUser(_juan.UserId));
        var useCase = new MarkNotificationReadUseCase(_repo, _clock);

        var first = await useCase.ExecuteAsync(_juan, n.Id, default);
        _clock.Now = _clock.Now.AddMinutes(10);
        var second = await useCase.ExecuteAsync(_juan, n.Id, default);

        Assert.Equal(first.ReadAt, second.ReadAt);
    }

    [Fact]
    public async Task A_notification_of_somebody_else_or_of_another_role_is_not_found()
    {
        var personal = await Add(Audience.ForUser(_ana.UserId));
        var adminOnly = await Add(Audience.ForRole(Roles.Admin));
        var useCase = new MarkNotificationReadUseCase(_repo, _clock);

        await Assert.ThrowsAsync<NotificationNotFoundException>(() => useCase.ExecuteAsync(_juan, personal.Id, default));
        await Assert.ThrowsAsync<NotificationNotFoundException>(() => useCase.ExecuteAsync(_juan, adminOnly.Id, default));
        await Assert.ThrowsAsync<NotificationNotFoundException>(() => useCase.ExecuteAsync(_juan, Guid.NewGuid(), default));
        Assert.Empty(_repo.Reads);
    }

    [Fact]
    public async Task Mark_all_read_marks_only_my_visible_unread_ones()
    {
        await Add(Audience.ForUser(_juan.UserId));
        await Add(Audience.ForRole(Roles.User));
        await Add(Audience.ForRole(Roles.Admin));       // Juan is not ADMIN
        await Add(Audience.ForUser(_ana.UserId));

        Assert.Equal(2, await new MarkAllNotificationsReadUseCase(_repo, _clock).ExecuteAsync(_juan, default));
        Assert.Equal(0, await new MarkAllNotificationsReadUseCase(_repo, _clock).ExecuteAsync(_juan, default));
        Assert.Equal(2, (await List().ExecuteAsync(_juan, false, null, null, default)).Count(n => n.IsRead));
        Assert.Empty(await List().ExecuteAsync(_juan, true, null, null, default));
        Assert.Equal(2, (await new GetUnreadCountUseCase(_repo).ExecuteAsync(_ana, default)).Count);   // Ana: personal + USER
    }
}
