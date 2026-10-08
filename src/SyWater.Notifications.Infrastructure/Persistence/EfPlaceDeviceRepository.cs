using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Devices;
using SyWater.Notifications.Infrastructure.Persistence.Entities;

namespace SyWater.Notifications.Infrastructure.Persistence;

public sealed class EfPlaceDeviceRepository(NotificationDbContext db) : IPlaceDeviceRepository
{
    public async Task<PlaceDevice?> GetByPlaceAsync(Guid placeId, CancellationToken ct)
    {
        var row = await db.PlaceDevices.AsNoTracking().FirstOrDefaultAsync(d => d.PlaceId == placeId, ct);
        return row is null ? null : ToDomain(row);
    }

    /// <summary>DELETE the old row of that place or device + INSERT the new one, in one transaction.</summary>
    public async Task ReplaceAsync(PlaceDevice device, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.PlaceDevices.Where(d => d.PlaceId == device.PlaceId || d.DeviceId == device.DeviceId).ExecuteDeleteAsync(ct);
        db.PlaceDevices.Add(new PlaceDeviceEntity
        {
            PlaceId = device.PlaceId,
            DeviceId = device.DeviceId,
            UserId = device.UserId,
            SerialNumber = device.SerialNumber,
            ValveState = device.ValveState is null ? null : ToDb(device.ValveState.Value),
            ValveReportedAt = device.ValveReportedAt,
            LastReadingAt = device.LastReadingAt,
            OfflineAlertedAt = device.OfflineAlertedAt,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public Task RemoveAsync(Guid placeId, Guid deviceId, CancellationToken ct) =>
        db.PlaceDevices.Where(d => d.PlaceId == placeId && d.DeviceId == deviceId).ExecuteDeleteAsync(ct);

    /// <summary>The WHERE keeps the newest report if two arrive out of order.</summary>
    public Task SaveValveReportAsync(PlaceDevice device, CancellationToken ct) =>
        db.PlaceDevices
            .Where(d => d.PlaceId == device.PlaceId && d.DeviceId == device.DeviceId
                        && (d.ValveReportedAt == null || d.ValveReportedAt < device.ValveReportedAt))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.ValveState, device.ValveState == null ? null : ToDb(device.ValveState.Value))
                .SetProperty(d => d.ValveReportedAt, device.ValveReportedAt), ct);

    /// <summary>The WHERE keeps the newest reading if two arrive out of order; a reading always re-arms the offline alert.</summary>
    public Task SaveReadingAsync(PlaceDevice device, CancellationToken ct) =>
        db.PlaceDevices
            .Where(d => d.PlaceId == device.PlaceId && d.DeviceId == device.DeviceId
                        && (d.LastReadingAt == null || d.LastReadingAt < device.LastReadingAt))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LastReadingAt, device.LastReadingAt)
                .SetProperty(d => d.OfflineAlertedAt, (DateTime?)null), ct);

    public async Task<IReadOnlyList<PlaceDevice>> FindSilentAsync(DateTime cutoffUtc, int max, CancellationToken ct)
    {
        var rows = await db.PlaceDevices.AsNoTracking()
            .Where(d => d.OfflineAlertedAt == null && d.LastReadingAt != null && d.LastReadingAt <= cutoffUtc)
            .OrderBy(d => d.LastReadingAt).Take(max).ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    /// <summary>The WHERE on the last reading: if the device reported while we were notifying, nothing is marked.</summary>
    public async Task<bool> MarkOfflineAlertedAsync(PlaceDevice device, DateTime now, CancellationToken ct) =>
        await db.PlaceDevices
            .Where(d => d.PlaceId == device.PlaceId && d.DeviceId == device.DeviceId
                        && d.OfflineAlertedAt == null && d.LastReadingAt == device.LastReadingAt)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.OfflineAlertedAt, now), ct) > 0;

    private static PlaceDevice ToDomain(PlaceDeviceEntity e) => PlaceDevice.Restore(
        e.PlaceId, e.DeviceId, e.UserId, e.SerialNumber,
        e.ValveState switch { "OPEN" => ValveStatus.Open, "CLOSED" => ValveStatus.Closed, _ => null },
        e.ValveReportedAt is null ? null : NotificationMapper.Utc(e.ValveReportedAt.Value),
        e.LastReadingAt is null ? null : NotificationMapper.Utc(e.LastReadingAt.Value),
        e.OfflineAlertedAt is null ? null : NotificationMapper.Utc(e.OfflineAlertedAt.Value));

    private static string ToDb(ValveStatus s) => s == ValveStatus.Closed ? "CLOSED" : "OPEN";
}
