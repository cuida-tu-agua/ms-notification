using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Notifications.Infrastructure.Persistence.Entities;

/// <summary>1:1 copy of notification.notification_preferences (release v1.2 of ms-notification-db; key: user_id + severity).</summary>
[Table("notification_preferences", Schema = "notification")]
public sealed class NotificationPreferenceEntity
{
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("severity")] public string Severity { get; set; } = "";
    [Column("in_app")] public bool InApp { get; set; }
    [Column("push")] public bool Push { get; set; }
    [Column("email")] public bool Email { get; set; }
    [Column("sms")] public bool Sms { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
