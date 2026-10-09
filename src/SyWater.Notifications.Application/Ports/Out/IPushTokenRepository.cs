using SyWater.Notifications.Domain.Push;

namespace SyWater.Notifications.Application.Ports.Out;

/// <summary>Persistence of the Expo push tokens (HU-026).</summary>
public interface IPushTokenRepository
{
    /// <summary>The token is unique: if it belonged to another user (shared phone) it changes owner.</summary>
    Task UpsertAsync(Guid userId, string token, PushPlatform platform, DateTime now, CancellationToken ct);

    /// <summary>Removes the token ONLY if it belongs to that user. Idempotent.</summary>
    Task RemoveAsync(Guid userId, string token, CancellationToken ct);

    Task<IReadOnlyList<PushToken>> ListForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>Expo said these tokens are dead (app uninstalled): forget them.</summary>
    Task RemoveTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken ct);
}
