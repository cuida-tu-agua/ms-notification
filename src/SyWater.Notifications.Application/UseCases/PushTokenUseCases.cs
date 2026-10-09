using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Push;

namespace SyWater.Notifications.Application.UseCases;

public sealed class RegisterPushTokenUseCase(IPushTokenRepository tokens, TimeProvider clock) : IRegisterPushTokenUseCase
{
    public Task ExecuteAsync(Requester requester, string? token, string? platform, CancellationToken ct) =>
        tokens.UpsertAsync(requester.UserId, PushToken.NormalizeToken(token), PushToken.ParsePlatform(platform),
            clock.GetUtcNow().UtcDateTime, ct);
}

public sealed class UnregisterPushTokenUseCase(IPushTokenRepository tokens) : IUnregisterPushTokenUseCase
{
    public Task ExecuteAsync(Requester requester, string? token, CancellationToken ct) =>
        tokens.RemoveAsync(requester.UserId, PushToken.NormalizeToken(token), ct);
}
