namespace SyWater.Notifications.Domain.Notifications;

/// <summary>
/// One message for an <see cref="Audience"/>. It is immutable once created: reading it is NOT a change here
/// (a role notification is read by each person separately, see notification_reads).
/// </summary>
public sealed class Notification
{
    public const int MaxTitleLength = 150;
    public const int MaxBodyLength = 1000;

    public Guid Id { get; }
    public Audience Audience { get; }
    public NotificationType Type { get; }
    public NotificationSeverity Severity { get; }
    public string Title { get; }
    public string Body { get; }
    public Guid? PlaceId { get; }

    /// <summary>Id of the source event (RabbitMQ envelope). Makes a re-delivered event create nothing twice.</summary>
    public string? SourceEventId { get; }
    public DateTime CreatedAt { get; }

    private Notification(Guid id, Audience audience, NotificationType type, NotificationSeverity severity, string title,
        string body, Guid? placeId, string? sourceEventId, DateTime createdAt)
    {
        Id = id;
        Audience = audience;
        Type = type;
        Severity = severity;
        Title = title;
        Body = body;
        PlaceId = placeId;
        SourceEventId = sourceEventId;
        CreatedAt = createdAt;
    }

    public static Notification Create(Audience audience, NotificationType type, NotificationSeverity severity,
        string title, string body, Guid? placeId, string? sourceEventId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidNotificationException("The title is required.");
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidNotificationException("The body is required.");
        if (title.Trim().Length > MaxTitleLength)
            throw new InvalidNotificationException($"The title has at most {MaxTitleLength} characters.");
        if (body.Trim().Length > MaxBodyLength)
            throw new InvalidNotificationException($"The body has at most {MaxBodyLength} characters.");

        return new Notification(Guid.NewGuid(), audience, type, severity, title.Trim(), body.Trim(), placeId,
            string.IsNullOrWhiteSpace(sourceEventId) ? null : sourceEventId.Trim(), now);
    }

    public static Notification Restore(Guid id, Audience audience, NotificationType type, NotificationSeverity severity,
        string title, string body, Guid? placeId, string? sourceEventId, DateTime createdAt) =>
        new(id, audience, type, severity, title, body, placeId, sourceEventId, createdAt);
}
