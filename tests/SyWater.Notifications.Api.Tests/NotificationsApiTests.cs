using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SyWater.Notifications.Domain.Notifications;

namespace SyWater.Notifications.Api.Tests;

public sealed class NotificationsApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();   // one app + database per test: broadcasts to a role must not leak between tests

    public NotificationsApiTests()
    {
        _factory.CreateSchema();
    }

    public void Dispose() => _factory.Dispose();

    private HttpClient Client(Guid user, params string[] roles)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.TokenFor(user, roles));
        return client;
    }

    private static async Task<string?> Title(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();

    [Fact]
    public async Task Without_a_token_every_endpoint_answers_401_but_health_is_open()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Each_person_sees_their_own_and_their_roles_notifications_only()
    {
        var juan = Guid.NewGuid();
        var ana = Guid.NewGuid();
        var admin = Guid.NewGuid();
        await _factory.SeedAsync(Audience.ForUser(juan), "for Juan");
        await _factory.SeedAsync(Audience.ForUser(ana), "for Ana");
        await _factory.SeedAsync(Audience.ForRole(Roles.Admin), "for admins");

        var juanInbox = await Client(juan, Roles.User).GetFromJsonAsync<JsonElement>("/api/notifications");
        Assert.Equal(["for Juan"], juanInbox.EnumerateArray().Select(n => n.GetProperty("title").GetString()));

        var adminTitles = (await Client(admin, Roles.User, Roles.Admin).GetFromJsonAsync<JsonElement>("/api/notifications"))
            .EnumerateArray().Select(n => n.GetProperty("title").GetString()).ToList();
        Assert.Contains("for admins", adminTitles);
        Assert.DoesNotContain("for Juan", adminTitles);
        Assert.DoesNotContain("for Ana", adminTitles);
    }

    [Fact]
    public async Task Read_flow_counter_goes_down_and_read_all_marks_the_rest()
    {
        var me = Guid.NewGuid();
        await _factory.SeedAsync(Audience.ForUser(me), "one", NotificationSeverity.Critical);
        await _factory.SeedAsync(Audience.ForUser(me), "two");
        var client = Client(me, Roles.User);

        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count")).GetProperty("count").GetInt32());

        var first = (await client.GetFromJsonAsync<JsonElement>("/api/notifications")).EnumerateArray()
            .Single(n => n.GetProperty("title").GetString() == "one");
        Assert.Equal("CRITICAL", first.GetProperty("severity").GetString());   // enums travel as strings
        var read = await client.PostAsync($"/api/notifications/{first.GetProperty("id").GetGuid()}/read", null);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True((await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isRead").GetBoolean());
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count")).GetProperty("count").GetInt32());
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/notifications?unreadOnly=true")).EnumerateArray());

        var all = await client.PostAsync("/api/notifications/read-all", null);
        Assert.Equal(1, (await all.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("marked").GetInt32());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count")).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Marking_somebody_elses_notification_is_404_with_the_stable_code()
    {
        var owner = Guid.NewGuid();
        await _factory.SeedAsync(Audience.ForUser(owner), "private");
        var id = (await Client(owner, Roles.User).GetFromJsonAsync<JsonElement>("/api/notifications"))
            .EnumerateArray().Single().GetProperty("id").GetGuid();

        var stranger = await Client(Guid.NewGuid(), Roles.User, Roles.Admin).PostAsync($"/api/notifications/{id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal("notification.not_found", await Title(stranger));
        Assert.Equal(1, (await Client(owner, Roles.User).GetFromJsonAsync<JsonElement>("/api/notifications/unread-count"))
            .GetProperty("count").GetInt32());   // the owner still has it unread
    }

    [Fact]
    public async Task A_token_without_roles_only_sees_personal_notifications()
    {
        var me = Guid.NewGuid();
        await _factory.SeedAsync(Audience.ForRole(Roles.User), "broadcast");
        await _factory.SeedAsync(Audience.ForUser(me), "mine");

        var titles = (await Client(me).GetFromJsonAsync<JsonElement>("/api/notifications"))
            .EnumerateArray().Select(n => n.GetProperty("title").GetString());

        Assert.Equal(["mine"], titles);
    }
}
