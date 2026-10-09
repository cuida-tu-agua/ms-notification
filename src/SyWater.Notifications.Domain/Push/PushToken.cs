using SyWater.Notifications.Domain.Common;

namespace SyWater.Notifications.Domain.Push;

public enum PushPlatform { Android, Ios }

/// <summary>The Expo push token of ONE phone where a user is signed in (HU-026).</summary>
public sealed record PushToken(Guid Id, Guid UserId, string Token, PushPlatform Platform, DateTime CreatedAt, DateTime LastSeenAt)
{
    public const int MaxLength = 200;
    private static readonly string[] Prefixes = ["ExponentPushToken[", "ExpoPushToken["];

    /// <summary>Expo tokens look like ExponentPushToken[xxxx]. Anything else is rejected before it is stored.</summary>
    public static string NormalizeToken(string? token)
    {
        var value = token?.Trim() ?? "";
        var prefix = Prefixes.FirstOrDefault(p => value.StartsWith(p, StringComparison.Ordinal));
        if (prefix is null || !value.EndsWith(']') || value.Length > MaxLength
            || value.Length == prefix.Length + 1 || value.Any(char.IsWhiteSpace))
            throw new InvalidPushTokenException();
        return value;
    }

    public static PushPlatform ParsePlatform(string? platform) => platform?.Trim().ToUpperInvariant() switch
    {
        "ANDROID" => PushPlatform.Android,
        "IOS" => PushPlatform.Ios,
        _ => throw new InvalidPushPlatformException(platform),
    };

    public static string PlatformToDb(PushPlatform platform) => platform == PushPlatform.Ios ? "IOS" : "ANDROID";
}

public sealed class InvalidPushTokenException()
    : DomainException("push.invalid_token", "The push token is not a valid Expo push token (ExponentPushToken[...]).");

public sealed class InvalidPushPlatformException(string? platform)
    : DomainException("push.invalid_platform", $"Push platform '{platform}' is not supported. Use ANDROID or IOS.");
