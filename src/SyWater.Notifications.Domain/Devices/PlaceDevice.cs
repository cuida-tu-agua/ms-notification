namespace SyWater.Notifications.Domain.Devices;

/// <summary>What the device last reported for its valve (it never reports "unknown").</summary>
public enum ValveStatus { Open, Closed }

/// <summary>What a valve report means for the owner.</summary>
public enum ValveChange { None, Closed, Opened }

/// <summary>Result of <see cref="PlaceDevice.ApplyValveReport"/>: <c>Applied</c> = it must be saved.</summary>
public readonly record struct ValveReportResult(bool Applied, ValveChange Change);

/// <summary>
/// Who owns the device of a place (learned from device.linked) and the last valve state it reported.
/// device.valve.reported carries no user id: this is how the consumer finds the person to notify, and
/// the stored state is how it notifies only CHANGES (the device reports periodically, not only when it moves).
/// </summary>
public sealed class PlaceDevice
{
    public Guid PlaceId { get; }
    public Guid DeviceId { get; }
    public Guid UserId { get; }
    public string SerialNumber { get; }
    public ValveStatus? ValveState { get; private set; }
    public DateTime? ValveReportedAt { get; private set; }

    /// <summary>Last reading received (SERVER time, not the ESP32 clock). null = it never reported.</summary>
    public DateTime? LastReadingAt { get; private set; }

    /// <summary>When the owner was told the device went silent; null = not alerted. Cleared by the next reading.</summary>
    public DateTime? OfflineAlertedAt { get; private set; }

    /// <summary>Readings arrive every few seconds: the last-seen date is saved at most this often.</summary>
    public static readonly TimeSpan SaveReadingEvery = TimeSpan.FromSeconds(30);

    private PlaceDevice(Guid placeId, Guid deviceId, Guid userId, string serialNumber, ValveStatus? valveState, DateTime? valveReportedAt,
        DateTime? lastReadingAt, DateTime? offlineAlertedAt)
    {
        PlaceId = placeId;
        DeviceId = deviceId;
        UserId = userId;
        SerialNumber = serialNumber;
        ValveState = valveState;
        ValveReportedAt = valveReportedAt;
        LastReadingAt = lastReadingAt;
        OfflineAlertedAt = offlineAlertedAt;
    }

    public static PlaceDevice ForNewLink(Guid placeId, Guid deviceId, Guid userId, string serialNumber) =>
        new(placeId, deviceId, userId, serialNumber, null, null, null, null);

    public static PlaceDevice Restore(Guid placeId, Guid deviceId, Guid userId, string serialNumber,
        ValveStatus? valveState, DateTime? valveReportedAt, DateTime? lastReadingAt = null, DateTime? offlineAlertedAt = null) =>
        new(placeId, deviceId, userId, serialNumber, valveState, valveReportedAt, lastReadingAt, offlineAlertedAt);

    /// <summary>
    /// Older or repeated reports (RabbitMQ does not promise order) are ignored. A report is a CHANGE when the
    /// valve closes (also when it was never reported before: the owner must know the water is cut) or when it
    /// opens after being closed. A first "open" report is not news.
    /// </summary>
    public ValveReportResult ApplyValveReport(ValveStatus reported, DateTime at)
    {
        if (ValveReportedAt is { } last && at <= last) return new(false, ValveChange.None);

        var previous = ValveState;
        ValveState = reported;
        ValveReportedAt = at;

        var change = (previous, reported) switch
        {
            (not ValveStatus.Closed, ValveStatus.Closed) => ValveChange.Closed,
            (ValveStatus.Closed, ValveStatus.Open) => ValveChange.Opened,
            _ => ValveChange.None,
        };
        return new(true, change);
    }

    /// <summary>
    /// A reading arrived. Older or repeated ones are ignored. Reconnecting clears the "already alerted" mark so a
    /// future silence alerts again. Returns true when it must be saved: always after an alert, otherwise at most
    /// every <see cref="SaveReadingEvery"/> (the threshold is minutes, so that precision is plenty).
    /// </summary>
    public bool ApplyReading(DateTime receivedAt)
    {
        if (LastReadingAt is { } last && receivedAt <= last) return false;

        var gap = LastReadingAt is { } previous ? receivedAt - previous : TimeSpan.MaxValue;
        var wasAlerted = OfflineAlertedAt is not null;
        LastReadingAt = receivedAt;
        OfflineAlertedAt = null;
        return wasAlerted || gap >= SaveReadingEvery;
    }

    /// <summary>
    /// HU-032: it reported at least once, has been silent for <paramref name="after"/> (default 10 min) and the owner
    /// was not told yet. A device that never reported is shown as "never reported" (HU-013), not alerted.
    /// </summary>
    public bool IsSilent(DateTime now, TimeSpan after) =>
        OfflineAlertedAt is null && LastReadingAt is { } last && now - last >= after;

    public void MarkOfflineAlerted(DateTime now) => OfflineAlertedAt = now;
}
