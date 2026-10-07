using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Domain.Tests;

public class NotificationDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    private static Notification Make(Audience audience, string title = "Valve closed", string body = "Your valve is closed.") =>
        Notification.Create(audience, NotificationType.ValveChanged, NotificationSeverity.Info, title, body, null, "evt-1", Now);

    [Fact]
    public void A_user_audience_includes_only_that_user()
    {
        var me = Guid.NewGuid();
        var audience = Audience.ForUser(me);
        Assert.True(audience.Includes(me, []));
        Assert.False(audience.Includes(Guid.NewGuid(), [Roles.Admin]));   // a role does not open a personal notification
    }

    [Fact]
    public void A_role_audience_includes_everyone_with_that_role_ignoring_case()
    {
        var audience = Audience.ForRole(" admin ");
        Assert.Equal("ADMIN", audience.Role);
        Assert.True(audience.Includes(Guid.NewGuid(), ["admin"]));
        Assert.False(audience.Includes(Guid.NewGuid(), [Roles.User]));
    }

    [Fact]
    public void An_audience_is_a_user_or_a_role_never_both_or_none()
    {
        Assert.Throws<InvalidAudienceException>(() => Audience.ForUser(Guid.Empty));
        Assert.Throws<InvalidAudienceException>(() => Audience.ForRole("  "));
        Assert.Throws<InvalidAudienceException>(() => Audience.Restore(Guid.NewGuid(), "ADMIN"));
        Assert.Throws<InvalidAudienceException>(() => Audience.Restore(null, null));
    }

    [Fact]
    public void Create_trims_text_and_keeps_the_source_event()
    {
        var n = Make(Audience.ForUser(Guid.NewGuid()), "  Valve closed  ", "  Done.  ");
        Assert.Equal("Valve closed", n.Title);
        Assert.Equal("Done.", n.Body);
        Assert.Equal("evt-1", n.SourceEventId);
        Assert.Equal(Now, n.CreatedAt);
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData("title", "  ")]
    public void Title_and_body_are_required(string title, string body) =>
        Assert.Throws<InvalidNotificationException>(() => Make(Audience.ForRole("ADMIN"), title, body));

    [Fact]
    public void Title_and_body_have_a_maximum_length()
    {
        var audience = Audience.ForRole("ADMIN");
        Assert.Throws<InvalidNotificationException>(() => Make(audience, new string('a', Notification.MaxTitleLength + 1)));
        Assert.Throws<InvalidNotificationException>(() => Make(audience, body: new string('a', Notification.MaxBodyLength + 1)));
    }
}
