using SyWater.Notifications.Domain.Devices;

namespace SyWater.Notifications.Domain.Tests;

public class DeviceLivenessTests
{
    private static readonly DateTime T0 = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private static PlaceDevice NewLink() => PlaceDevice.ForNewLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SW-1");

    [Fact]
    public void A_device_that_never_reported_is_not_silent_it_is_just_never_reported()
    {
        Assert.False(NewLink().IsSilent(T0.AddDays(1), TenMinutes));
    }

    [Fact]
    public void It_is_silent_exactly_when_the_threshold_is_reached_and_only_until_it_is_alerted()
    {
        var device = NewLink();
        device.ApplyReading(T0);

        Assert.False(device.IsSilent(T0.AddMinutes(9), TenMinutes));
        Assert.True(device.IsSilent(T0.AddMinutes(10), TenMinutes));

        device.MarkOfflineAlerted(T0.AddMinutes(10));
        Assert.False(device.IsSilent(T0.AddMinutes(30), TenMinutes));   // ONE alert until it reports again
    }

    [Fact]
    public void The_next_reading_re_arms_the_alert_and_must_be_saved()
    {
        var device = NewLink();
        device.ApplyReading(T0);
        device.MarkOfflineAlerted(T0.AddMinutes(10));

        Assert.True(device.ApplyReading(T0.AddMinutes(11)));            // it was alerted: save at once
        Assert.Null(device.OfflineAlertedAt);
        Assert.True(device.IsSilent(T0.AddMinutes(21), TenMinutes));    // silent again: a new alert later
    }

    [Fact]
    public void Frequent_readings_are_saved_at_most_every_30_seconds()
    {
        var device = NewLink();
        Assert.True(device.ApplyReading(T0));                       // first one
        Assert.False(device.ApplyReading(T0.AddSeconds(5)));        // counted in memory, not worth a write
        Assert.Equal(T0.AddSeconds(5), device.LastReadingAt);
        Assert.False(device.ApplyReading(T0.AddSeconds(20)));
        Assert.True(device.ApplyReading(T0.AddSeconds(60)));
    }

    [Fact]
    public void Old_or_repeated_readings_are_ignored()
    {
        var device = NewLink();
        device.ApplyReading(T0);

        Assert.False(device.ApplyReading(T0));
        Assert.False(device.ApplyReading(T0.AddSeconds(-5)));
        Assert.Equal(T0, device.LastReadingAt);
    }
}
