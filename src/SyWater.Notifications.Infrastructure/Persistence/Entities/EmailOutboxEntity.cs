using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Notifications.Infrastructure.Persistence.Entities;

/// <summary>1:1 copy of notification.email_outbox (release v1.3 of ms-notification-db).</summary>
[Table("email_outbox", Schema = "notification")]
public sealed class EmailOutboxEntity
{
    [Key, Column("id"), DatabaseGenerated(DatabaseGeneratedOption.None)] public Guid Id { get; set; }
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("source_event_id")] public string? SourceEventId { get; set; }
    [Column("subject")] public string Subject { get; set; } = "";
    [Column("text_body")] public string TextBody { get; set; } = "";
    [Column("html_body")] public string HtmlBody { get; set; } = "";
    [Column("status")] public string Status { get; set; } = "PENDING";
    [Column("attempts")] public int Attempts { get; set; }
    [Column("next_attempt_at")] public DateTime NextAttemptAt { get; set; }
    [Column("last_error")] public string? LastError { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("sent_at")] public DateTime? SentAt { get; set; }
}
