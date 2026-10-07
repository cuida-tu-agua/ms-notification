using Microsoft.AspNetCore.Mvc;
using SyWater.Notifications.Api.Security;
using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Views;

namespace SyWater.Notifications.Api.Controllers;

/// <summary>Answer of POST …/read-all.</summary>
public sealed record MarkAllReadResponse(int Marked);

/// <summary>
/// Notification center. Every endpoint requires a valid token (fallback policy in Program.cs); the inbox is
/// always the one of the token's user and roles, never of a user id sent by the client.
/// </summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    /// <summary>My inbox, newest first. Page with <c>before</c> = CreatedAt of the last item received (ISO 8601, UTC).</summary>
    [HttpGet]
    public Task<IReadOnlyList<NotificationView>> List(
        [FromQuery] bool unreadOnly, [FromQuery] DateTimeOffset? before, [FromQuery] int? limit,
        [FromServices] IListMyNotificationsUseCase useCase, CancellationToken ct) =>
        useCase.ExecuteAsync(User.GetRequester(), unreadOnly, before?.UtcDateTime, limit, ct);

    /// <summary>The number on the bell icon.</summary>
    [HttpGet("unread-count")]
    public Task<UnreadCountView> UnreadCount([FromServices] IGetUnreadCountUseCase useCase, CancellationToken ct) =>
        useCase.ExecuteAsync(User.GetRequester(), ct);

    /// <summary>Marks one as read (idempotent). 404 if it is not mine.</summary>
    [HttpPost("{id:guid}/read")]
    public Task<NotificationView> MarkRead(Guid id, [FromServices] IMarkNotificationReadUseCase useCase, CancellationToken ct) =>
        useCase.ExecuteAsync(User.GetRequester(), id, ct);

    /// <summary>Marks every unread notification of mine.</summary>
    [HttpPost("read-all")]
    public async Task<MarkAllReadResponse> MarkAllRead([FromServices] IMarkAllNotificationsReadUseCase useCase, CancellationToken ct) =>
        new(await useCase.ExecuteAsync(User.GetRequester(), ct));
}
