using Microsoft.AspNetCore.Mvc;
using SyWater.Notifications.Api.Security;
using SyWater.Notifications.Application.Ports.In;

namespace SyWater.Notifications.Api.Controllers;

public sealed record PushTokenRequest(string? Token, string? Platform);
public sealed record RemovePushTokenRequest(string? Token);

/// <summary>HU-026 · the phones of the signed-in user that receive push alerts. Always the token's user.</summary>
[ApiController]
[Route("api/push-tokens")]
public sealed class PushTokensController : ControllerBase
{
    /// <summary>Called after login once the user allowed notifications. Idempotent. 400 if the token is not an Expo token.</summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] PushTokenRequest body, [FromServices] IRegisterPushTokenUseCase useCase, CancellationToken ct)
    {
        await useCase.ExecuteAsync(User.GetRequester(), body.Token, body.Platform, ct);
        return NoContent();
    }

    /// <summary>Called on logout. Idempotent: removing a token that is not mine (or not there) is also 204.</summary>
    [HttpDelete]
    public async Task<IActionResult> Remove([FromBody] RemovePushTokenRequest body, [FromServices] IUnregisterPushTokenUseCase useCase, CancellationToken ct)
    {
        await useCase.ExecuteAsync(User.GetRequester(), body.Token, ct);
        return NoContent();
    }
}
