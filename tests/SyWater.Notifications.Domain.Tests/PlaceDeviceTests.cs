using SyWater.Notifications.Domain.Devices;

namespace SyWater.Notifications.Domain.Tests;

public class PlaceDeviceTests
{
    private static readonly DateTime T0 = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private static PlaceDevice NewLink() => PlaceDevice.ForNewLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SW-ESP32-000001");

    [Fact]
    public void A_first_closed_report_is_news_because_the_water_is_cut()
    {
        var result = NewLink().ApplyValveReport(ValveStatus.Closed, T0);

        Assert.True(result.Applied);
        Assert.Equal(ValveChange.Closed, result.Change);
    }

    [Fact]
    public void A_first_open_report_is_saved_but_is_not_news()
    {
        var result = NewLink().ApplyValveReport(ValveStatus.Open, T0);

        Assert.True(result.Applied);
        Assert.Equal(ValveChange.None, result.Change);
    }

    [Fact]
    public void Periodic_reports_of_the_same_state_are_not_news()
    {
        var device = NewLink();
        device.ApplyValveReport(ValveStatus.Closed, T0);

        var again = device.ApplyValveReport(ValveStatus.Closed, T0.AddMinutes(1));

        Assert.True(again.Applied);   // the date moves forward
        Assert.Equal(ValveChange.None, again.Change);
    }

    [Fact]
    public void Opening_after_a_closure_is_news_and_closing_again_too()
    {
        var device = NewLink();
        device.ApplyValveReport(ValveStatus.Closed, T0);

        Assert.Equal(ValveChange.Opened, device.ApplyValveReport(ValveStatus.Open, T0.AddMinutes(5)).Change);
        Assert.Equal(ValveChange.Closed, device.ApplyValveReport(ValveStatus.Closed, T0.AddMinutes(10)).Change);
    }

    [Fact]
    public void Old_or_repeated_reports_are_ignored()
    {
        var device = NewLink();
        device.ApplyValveReport(ValveStatus.Open, T0);

        Assert.False(device.ApplyValveReport(ValveStatus.Closed, T0).Applied);                 // same instant (redelivery)
        Assert.False(device.ApplyValveReport(ValveStatus.Closed, T0.AddSeconds(-30)).Applied); // out of order
        Assert.Equal(ValveStatus.Open, device.ValveState);
    }
}
