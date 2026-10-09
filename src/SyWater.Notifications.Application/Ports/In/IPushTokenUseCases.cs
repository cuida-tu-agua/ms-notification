using SyWater.Notifications.Application.Views;

namespace SyWater.Notifications.Application.Ports.In;

/// <summary>HU-026: the phone tells which Expo token receives my alerts (after login / permission granted).</summary>
public interface IRegisterPushTokenUseCase
{
    Task ExecuteAsync(Requester requester, string? token, string? platform, CancellationToken ct);
}

/// <summary>HU-026: the phone stops receiving my alerts (logout). Idempotent.</summary>
public interface IUnregisterPushTokenUseCase
{
    Task ExecuteAsync(Requester requester, string? token, CancellationToken ct);
}
