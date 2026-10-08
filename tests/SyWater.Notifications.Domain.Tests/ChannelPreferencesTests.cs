using SyWater.Notifications.Domain.Notifications;
using SyWater.Notifications.Domain.Preferences;

namespace SyWater.Notifications.Domain.Tests;

public class ChannelPreferencesTests
{
    [Fact]
    public void Defaults_follow_the_channel_matrix()
    {
        var prefs = ChannelPreferences.Defaults();

        Assert.Equal(NotificationChannels.All, prefs.ChannelsFor(NotificationSeverity.Critical));
        Assert.Equal(NotificationChannels.InApp | NotificationChannels.Push, prefs.ChannelsFor(NotificationSeverity.Warning));
        Assert.Equal(NotificationChannels.InApp, prefs.ChannelsFor(NotificationSeverity.Info));
    }

    [Fact]
    public void Critical_always_keeps_in_app()
    {
        var prefs = ChannelPreferences.Defaults();

        Assert.Throws<CriticalChannelRequiredException>(() => prefs.Set(NotificationSeverity.Critical, NotificationChannels.Email));
        Assert.Throws<CriticalChannelRequiredException>(() => prefs.Set(NotificationSeverity.Critical, NotificationChannels.None));
        prefs.Set(NotificationSeverity.Critical, NotificationChannels.InApp);
        Assert.Equal(NotificationChannels.InApp, prefs.ChannelsFor(NotificationSeverity.Critical));
    }

    [Fact]
    public void A_level_can_be_changed_without_touching_the_others()
    {
        var prefs = ChannelPreferences.Defaults();
        prefs.Set(NotificationSeverity.Warning, NotificationChannels.None);

        Assert.Equal(NotificationChannels.None, prefs.ChannelsFor(NotificationSeverity.Warning));
        Assert.Equal(NotificationChannels.InApp, prefs.ChannelsFor(NotificationSeverity.Info));
    }
}
