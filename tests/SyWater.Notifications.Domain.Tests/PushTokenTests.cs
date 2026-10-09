using SyWater.Notifications.Domain.Push;

namespace SyWater.Notifications.Domain.Tests;

public class PushTokenTests
{
    [Theory]
    [InlineData("ExponentPushToken[abc123_-XYZ]")]
    [InlineData("ExpoPushToken[abc123]")]
    [InlineData("  ExponentPushToken[abc123]  ")]
    public void An_expo_token_is_accepted_and_trimmed(string token) =>
        Assert.Equal(token.Trim(), PushToken.NormalizeToken(token));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc123")]
    [InlineData("ExponentPushToken[]")]
    [InlineData("ExponentPushToken[abc")]
    [InlineData("ExponentPushToken[a b]")]
    [InlineData("fcm:APA91bH...")]
    public void Anything_that_is_not_an_expo_token_is_rejected(string? token) =>
        Assert.Equal("push.invalid_token", Assert.Throws<InvalidPushTokenException>(() => PushToken.NormalizeToken(token)).Code);

    [Fact]
    public void A_token_longer_than_the_column_is_rejected()
    {
        var token = "ExponentPushToken[" + new string('a', PushToken.MaxLength) + "]";
        Assert.Throws<InvalidPushTokenException>(() => PushToken.NormalizeToken(token));
    }

    [Theory]
    [InlineData("android", PushPlatform.Android)]
    [InlineData("ANDROID", PushPlatform.Android)]
    [InlineData(" ios ", PushPlatform.Ios)]
    public void Android_and_ios_are_accepted_in_any_case(string value, PushPlatform expected) =>
        Assert.Equal(expected, PushToken.ParsePlatform(value));

    [Theory]
    [InlineData("web")]
    [InlineData("")]
    [InlineData(null)]
    public void The_web_has_no_push(string? value) =>
        Assert.Equal("push.invalid_platform", Assert.Throws<InvalidPushPlatformException>(() => PushToken.ParsePlatform(value)).Code);
}
