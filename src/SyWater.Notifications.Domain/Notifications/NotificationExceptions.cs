using SyWater.Notifications.Domain.Common;

namespace SyWater.Notifications.Domain.Notifications;

/// <summary>The notification does not exist or is not for you (same answer on purpose). HTTP 404.</summary>
public sealed class NotificationNotFoundException(Guid id)
    : DomainException("notification.not_found", $"Notification {id} was not found.");

/// <summary>Empty or too long title/body. HTTP 400.</summary>
public sealed class InvalidNotificationException(string message)
    : DomainException("notification.invalid", message);

/// <summary>An audience must be exactly one user or one role. HTTP 400.</summary>
public sealed class InvalidAudienceException(string message)
    : DomainException("notification.invalid_audience", message);
