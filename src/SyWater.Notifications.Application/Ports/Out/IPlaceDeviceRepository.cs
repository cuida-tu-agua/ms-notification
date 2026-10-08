using SyWater.Notifications.Domain.Devices;

namespace SyWater.Notifications.Application.Ports.Out;

/// <summary>Persistence of the owner and last valve state of the device of each place.</summary>
public interface IPlaceDeviceRepository
{
    Task<PlaceDevice?> GetByPlaceAsync(Guid placeId, CancellationToken ct);

    /// <summary>A device was linked: replaces any old row of that place or that device.</summary>
    Task ReplaceAsync(PlaceDevice device, DateTime now, CancellationToken ct);

    /// <summary>The device was unlinked: removes the row (only if it is still that device).</summary>
    Task RemoveAsync(Guid placeId, Guid deviceId, CancellationToken ct);

    /// <summary>Saves valve state + date, only if this report is newer than the stored one.</summary>
    Task SaveValveReportAsync(PlaceDevice device, CancellationToken ct);

    /// <summary>Saves last_reading_at (if newer) and clears the "already alerted" mark: the device is back.</summary>
    Task SaveReadingAsync(PlaceDevice device, CancellationToken ct);

    /// <summary>HU-032: devices that reported before, were last heard at or before <paramref name="cutoffUtc"/> and were not alerted yet.</summary>
    Task<IReadOnlyList<PlaceDevice>> FindSilentAsync(DateTime cutoffUtc, int max, CancellationToken ct);

    /// <summary>Marks the alert as sent, ONLY if the device did not report meanwhile (same last reading). True if marked.</summary>
    Task<bool> MarkOfflineAlertedAsync(PlaceDevice device, DateTime now, CancellationToken ct);
}
