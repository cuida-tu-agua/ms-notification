using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Infrastructure.Push;

namespace SyWater.Notifications.Infrastructure.Tests;

public class ExpoPushSenderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Bodies.Add(body);
            return answer(request, body);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static ExpoPushSender Sender(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://expo.test/push/send") }, NullLogger<ExpoPushSender>.Instance);

    private static PushMessage Message(string token) =>
        new(token, "Válvula cerrada", "La válvula se cerró.", new Dictionary<string, string> { ["placeId"] = "p1" });

    [Fact]
    public async Task It_posts_an_array_with_what_the_phone_needs_and_maps_each_ticket()
    {
        var handler = new StubHandler((_, _) => Json(HttpStatusCode.OK,
            """{"data":[{"status":"ok","id":"1"},{"status":"error","message":"gone","details":{"error":"DeviceNotRegistered"}},{"status":"error","details":{"error":"MessageRateExceeded"}}]}"""));

        var results = await Sender(handler).SendAsync([Message("ExponentPushToken[a]"), Message("ExponentPushToken[b]"), Message("ExponentPushToken[c]")], default);

        Assert.Equal([PushOutcome.Delivered, PushOutcome.DeviceNotRegistered, PushOutcome.Failed], results.Select(r => r.Outcome));
        Assert.Equal(["ExponentPushToken[a]", "ExponentPushToken[b]", "ExponentPushToken[c]"], results.Select(r => r.Token));

        var sent = JsonDocument.Parse(Assert.Single(handler.Bodies)).RootElement;
        Assert.Equal(3, sent.GetArrayLength());
        var first = sent[0];
        Assert.Equal("ExponentPushToken[a]", first.GetProperty("to").GetString());
        Assert.Equal("Válvula cerrada", first.GetProperty("title").GetString());
        Assert.Equal("high", first.GetProperty("priority").GetString());
        Assert.Equal("p1", first.GetProperty("data").GetProperty("placeId").GetString());
    }

    [Fact]
    public async Task More_than_a_hundred_phones_go_in_several_calls()
    {
        var handler = new StubHandler((_, body) =>
        {
            var count = JsonDocument.Parse(body).RootElement.GetArrayLength();
            return Json(HttpStatusCode.OK, "{\"data\":[" + string.Join(",", Enumerable.Repeat("{\"status\":\"ok\"}", count)) + "]}");
        });

        var results = await Sender(handler).SendAsync(Enumerable.Range(0, 250).Select(i => Message($"ExponentPushToken[{i}]")).ToList(), default);

        Assert.Equal(3, handler.Bodies.Count);
        Assert.Equal(250, results.Count);
        Assert.All(results, r => Assert.Equal(PushOutcome.Delivered, r.Outcome));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "oops")]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, """{"data":[]}""")]   // fewer tickets than messages: nothing can be trusted
    [InlineData(HttpStatusCode.OK, """{"errors":[{"code":"X"}]}""")]
    public async Task A_bad_answer_never_throws_and_marks_every_push_as_failed(HttpStatusCode status, string body)
    {
        var results = await Sender(new StubHandler((_, _) => Json(status, body))).SendAsync([Message("ExponentPushToken[a]")], default);

        Assert.Equal(PushOutcome.Failed, Assert.Single(results).Outcome);
    }

    [Fact]
    public async Task Expo_being_unreachable_never_throws()
    {
        var results = await Sender(new StubHandler((_, _) => throw new HttpRequestException("no network")))
            .SendAsync([Message("ExponentPushToken[a]")], default);

        Assert.Equal(PushOutcome.Failed, Assert.Single(results).Outcome);
    }
}
