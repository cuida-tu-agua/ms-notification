using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.UseCases;

/// <summary>
/// HU-026: sends a notification to every phone of its owner and forgets the phones Expo says are gone
/// (app uninstalled). The dispatcher only calls it when the user kept the push channel for that urgency level.
/// </summary>
public sealed class PushNotifier(IPushTokenRepository tokens, IPushSender sender, PushSettings settings)
{
    public bool Enabled => settings.Enabled;

    public async Task NotifyAsync(Guid userId, Notification notification, CancellationToken ct)
    {
        if (!settings.Enabled) return;

        var phones = await tokens.ListForUserAsync(userId, ct);
        if (phones.Count == 0) return;

        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString(),
            ["type"] = notification.Type.ToString(),
            ["severity"] = notification.Severity.ToString(),
        };
        if (notification.PlaceId is { } placeId) data["placeId"] = placeId.ToString();

        var results = await sender.SendAsync(
            phones.Select(p => new PushMessage(p.Token, notification.Title, notification.Body, data)).ToList(), ct);

        var dead = results.Where(r => r.Outcome == PushOutcome.DeviceNotRegistered).Select(r => r.Token).ToList();
        if (dead.Count > 0) await tokens.RemoveTokensAsync(dead, ct);
    }
}
