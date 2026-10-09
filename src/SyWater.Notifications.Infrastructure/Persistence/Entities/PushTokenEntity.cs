using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Notifications.Infrastructure.Persistence.Entities;

/// <summary>1:1 copy of notification.push_tokens (release v1.5 of ms-notification-db).</summary>
[Table("push_tokens", Schema = "notification")]
public sealed class PushTokenEntity
{
    [Key, Column("id"), DatabaseGenerated(DatabaseGeneratedOption.None)] public Guid Id { get; set; }
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("token")] public string Token { get; set; } = "";
    [Column("platform")] public string Platform { get; set; } = "ANDROID";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("last_seen_at")] public DateTime LastSeenAt { get; set; }
}
