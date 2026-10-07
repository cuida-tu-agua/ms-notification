using System.Security.Claims;
using SyWater.Notifications.Application.Views;

namespace SyWater.Notifications.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Who is asking: "sub" (security.users.id) and the "roles" claim of the ms-iam token (e.g. ["ADMIN","USER"]).
    /// It never comes from the URL or the body, so nobody can read the inbox of someone else.
    /// </summary>
    public static Requester GetRequester(this ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirstValue("sub"), out var id))
            throw new UnauthorizedAccessException("The token has no valid 'sub' claim.");

        var roles = user.FindAll("roles").Select(c => c.Value.Trim().ToUpperInvariant())
            .Where(r => r.Length > 0).Distinct().ToArray();
        return new Requester(id, roles);
    }
}
