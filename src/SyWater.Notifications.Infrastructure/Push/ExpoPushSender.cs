using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SyWater.Notifications.Application.Ports.Out;

namespace SyWater.Notifications.Infrastructure.Push;

/// <summary>
/// Expo push service: POST https://exp.host/--/api/v2/push/send with a JSON array (at most 100 messages per call).
/// Answer: {"data":[{"status":"ok"|"error","details":{"error":"DeviceNotRegistered"}}]}, one entry per message, same order.
/// Never throws for a delivery problem (Expo down, timeout, bad answer): the phones just get no push this time.
/// </summary>
public sealed class ExpoPushSender(HttpClient http, ILogger<ExpoPushSender> logger) : IPushSender
{
    public const int BatchSize = 100;
    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        var results = new List<PushResult>(messages.Count);
        foreach (var batch in messages.Chunk(BatchSize))
            results.AddRange(await SendBatchAsync(batch, ct));
        return results;
    }

    private async Task<IEnumerable<PushResult>> SendBatchAsync(PushMessage[] batch, CancellationToken ct)
    {
        var body = batch.Select(m => new
        {
            to = m.Token,
            title = m.Title,
            body = m.Body,
            data = m.Data,
            sound = "default",
            priority = "high",
            channelId = "alerts",
        });

        try
        {
            using var response = await http.PostAsJsonAsync("", body, Json, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Expo push answered {Status}; {Count} push(es) were not sent.", (int)response.StatusCode, batch.Length);
                return Failed(batch);
            }

            var answer = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!answer.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() != batch.Length)
            {
                logger.LogWarning("Expo push answered something unexpected; {Count} push(es) not confirmed.", batch.Length);
                return Failed(batch);
            }

            return batch.Zip(data.EnumerateArray(), (message, ticket) => new PushResult(message.Token, OutcomeOf(ticket))).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Expo push is not reachable; {Count} push(es) were not sent.", batch.Length);
            return Failed(batch);
        }
    }

    private static PushOutcome OutcomeOf(JsonElement ticket)
    {
        if (ticket.TryGetProperty("status", out var status) && status.GetString() == "ok") return PushOutcome.Delivered;
        if (ticket.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object
            && details.TryGetProperty("error", out var error) && error.GetString() == "DeviceNotRegistered")
            return PushOutcome.DeviceNotRegistered;
        return PushOutcome.Failed;
    }

    private static List<PushResult> Failed(IEnumerable<PushMessage> batch) =>
        batch.Select(m => new PushResult(m.Token, PushOutcome.Failed)).ToList();
}
