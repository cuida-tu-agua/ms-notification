using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Notifications.Infrastructure.Persistence.Entities;

/// <summary>1:1 copy of notification.place_devices (release v1.1 of ms-notification-db).</summary>
[Table("place_devices", Schema = "notification")]
public sealed class PlaceDeviceEntity
{
    [Key, Column("place_id"), DatabaseGenerated(DatabaseGeneratedOption.None)] public Guid PlaceId { get; set; }
    [Column("device_id")] public Guid DeviceId { get; set; }
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("serial_number")] public string SerialNumber { get; set; } = "";
    [Column("valve_state")] public string? ValveState { get; set; }
    [Column("valve_reported_at")] public DateTime? ValveReportedAt { get; set; }
    [Column("last_reading_at")] public DateTime? LastReadingAt { get; set; }
    [Column("offline_alerted_at")] public DateTime? OfflineAlertedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}
