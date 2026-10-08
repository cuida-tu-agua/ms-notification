using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SyWater.Notifications.Application.Ports.Out;

namespace SyWater.Notifications.Infrastructure.Http;

/// <summary>
/// ms-iam "GET /internal/users/{id}/contact" with X-Internal-Key (the same shared key the other services use between
/// themselves). Answer: {"id","email","fullName"}; 404 = the user does not exist (or was deleted).
/// </summary>
public sealed class HttpUserContactDirectory(HttpClient http, string internalKey) : IUserContactDirectory
{
    public const string ServiceName = "ms-iam";
    public const string InternalKeyHeader = "X-Internal-Key";

    public async Task<UserContact?> FindAsync(Guid userId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/users/{userId}/contact");
        request.Headers.Add(InternalKeyHeader, internalKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(ServiceName, ex);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
            if (!response.IsSuccessStatusCode) throw new ExternalServiceUnavailableException(ServiceName);

            try
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var email = json.TryGetProperty("email", out var e) ? e.GetString() : null;
                var name = json.TryGetProperty("fullName", out var n) ? n.GetString() : null;
                return string.IsNullOrWhiteSpace(email) ? null : new UserContact(userId, email, name);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new ExternalServiceUnavailableException(ServiceName, ex);
            }
        }
    }
}
