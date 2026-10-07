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

    private PlaceDevice(Guid placeId, Guid deviceId, Guid userId, string serialNumber, ValveStatus? valveState, DateTime? valveReportedAt)
    {
        PlaceId = placeId;
        DeviceId = deviceId;
        UserId = userId;
        SerialNumber = serialNumber;
        ValveState = valveState;
        ValveReportedAt = valveReportedAt;
    }

    public static PlaceDevice ForNewLink(Guid placeId, Guid deviceId, Guid userId, string serialNumber) =>
        new(placeId, deviceId, userId, serialNumber, null, null);

    public static PlaceDevice Restore(Guid placeId, Guid deviceId, Guid userId, string serialNumber,
        ValveStatus? valveState, DateTime? valveReportedAt) =>
        new(placeId, deviceId, userId, serialNumber, valveState, valveReportedAt);

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
}
