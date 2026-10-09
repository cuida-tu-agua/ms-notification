namespace SyWater.Notifications.Application.Ports.Out;

/// <summary>One push for one phone. <see cref="Data"/> travels with it so the app can open the right screen when it is tapped.</summary>
public sealed record PushMessage(string Token, string Title, string Body, IReadOnlyDictionary<string, string> Data);

public enum PushOutcome { Delivered, DeviceNotRegistered, Failed }

public sealed record PushResult(string Token, PushOutcome Outcome);

/// <summary>Expo push service. Push is best-effort: a delivery problem is reported in the results, it never throws.</summary>
public interface IPushSender
{
    Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct);
}

/// <summary>Settings of the push channel that the use cases need (not the Expo ones).</summary>
public sealed record PushSettings(bool Enabled);
