using SyWater.Notifications.Domain.Notifications;
using SyWater.Notifications.Domain.Preferences;

namespace SyWater.Notifications.Application.Ports.Out;

/// <summary>Persistence of the channels each user chose per urgency level (HU-034).</summary>
public interface INotificationPreferenceRepository
{
    /// <summary>The user's saved levels; the ones never saved use the defaults.</summary>
    Task<ChannelPreferences> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>Insert-or-update of ONE level.</summary>
    Task SaveAsync(Guid userId, NotificationSeverity severity, NotificationChannels channels, DateTime now, CancellationToken ct);
}
