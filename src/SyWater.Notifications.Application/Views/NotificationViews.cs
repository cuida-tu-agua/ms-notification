using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.Views;

/// <summary>Who is asking (from the token): the id and the roles decide which notifications they see.</summary>
public sealed record Requester(Guid UserId, IReadOnlyCollection<string> Roles);

/// <summary>A notification plus whether the requester already read it (null = unread).</summary>
public sealed record NotificationWithRead(Notification Notification, DateTime? ReadAt);

/// <summary>One line of the inbox.</summary>
public sealed record NotificationView(
    Guid Id,
    NotificationType Type,
    NotificationSeverity Severity,
    string Title,
    string Body,
    Guid? PlaceId,
    DateTime CreatedAt,
    bool IsRead,
    DateTime? ReadAt)
{
    public static NotificationView From(NotificationWithRead item) => new(
        item.Notification.Id, item.Notification.Type, item.Notification.Severity, item.Notification.Title,
        item.Notification.Body, item.Notification.PlaceId, item.Notification.CreatedAt, item.ReadAt is not null, item.ReadAt);
}

public sealed record UnreadCountView(int Count);
