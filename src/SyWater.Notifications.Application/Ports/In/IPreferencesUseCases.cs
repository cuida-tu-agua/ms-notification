using SyWater.Notifications.Application.Views;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Application.Ports.In;

/// <summary>HU-034: my channels per urgency level.</summary>
public interface IGetMyPreferencesUseCase
{
    Task<PreferencesView> ExecuteAsync(Requester requester, CancellationToken ct);
}

/// <summary>HU-034: change the channels of ONE level. Applies from the next notification.</summary>
public interface IUpdateMyPreferencesUseCase
{
    Task<LevelPreferenceView> ExecuteAsync(Requester requester, NotificationSeverity severity, bool inApp, bool push, bool email, bool sms, CancellationToken ct);
}
