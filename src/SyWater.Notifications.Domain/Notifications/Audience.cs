using SyWater.Notifications.Domain.Common;

namespace SyWater.Notifications.Domain.Notifications;

/// <summary>
/// Who may see a notification: ONE user (contextual: "your valve closed") or EVERY user with a role
/// (broadcast: "maintenance tonight" for ADMIN). Exactly one of the two is set.
/// </summary>
public sealed record Audience
{
    public Guid? UserId { get; }
    public string? Role { get; }

    private Audience(Guid? userId, string? role)
    {
        UserId = userId;
        Role = role;
    }

    public static Audience ForUser(Guid userId) =>
        userId == Guid.Empty ? throw new InvalidAudienceException("A user audience needs a user id.") : new(userId, null);

    public static Audience ForRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) throw new InvalidAudienceException("A role audience needs a role.");
        return new(null, role.Trim().ToUpperInvariant());
    }

    /// <summary>Same rule the database CHECK enforces: user XOR role.</summary>
    public static Audience Restore(Guid? userId, string? role) =>
        (userId, role) switch
        {
            ({ } u, null) => ForUser(u),
            (null, { } r) => ForRole(r),
            _ => throw new InvalidAudienceException("An audience is a user or a role, never both or none."),
        };

    /// <summary>True if the person with this id and these roles must see the notification.</summary>
    public bool Includes(Guid userId, IReadOnlyCollection<string> roles) =>
        UserId is { } u ? u == userId : roles.Contains(Role!, StringComparer.OrdinalIgnoreCase);
}
