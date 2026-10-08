using Microsoft.AspNetCore.Mvc;
using SyWater.Notifications.Api.Security;
using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Api.Controllers;

/// <summary>Body of PUT …/preferences/{severity}: the four switches of that urgency level.</summary>
public sealed record UpdatePreferenceRequest(bool InApp, bool Push, bool Email, bool Sms);

/// <summary>HU-034 · choose the channels for each urgency level. Always the preferences of the token's user.</summary>
[ApiController]
[Route("api/notification-preferences")]
public sealed class PreferencesController : ControllerBase
{
    /// <summary>One entry per level (INFO, WARNING, CRITICAL); levels never changed show the default of the channel matrix.</summary>
    [HttpGet]
    public Task<PreferencesView> Get([FromServices] IGetMyPreferencesUseCase useCase, CancellationToken ct) =>
        useCase.ExecuteAsync(User.GetRequester(), ct);

    /// <summary>Applies from the next notification. 400 if a CRITICAL level would lose the in-app channel.</summary>
    [HttpPut("{severity}")]
    public Task<LevelPreferenceView> Update(NotificationSeverity severity, [FromBody] UpdatePreferenceRequest body,
        [FromServices] IUpdateMyPreferencesUseCase useCase, CancellationToken ct) =>
        useCase.ExecuteAsync(User.GetRequester(), severity, body.InApp, body.Push, body.Email, body.Sms, ct);
}
