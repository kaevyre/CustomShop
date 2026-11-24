using System.Security.Claims;
using System.Text.Json;

namespace Platform.ServiceDefaults.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>userId: claim "sub" (всі три провайдери) або NameIdentifier.</summary>
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>Ролі: claim "role" (Auth Service) та realm_access.roles (Keycloak).</summary>
    public static IEnumerable<string> GetRoles(this ClaimsPrincipal user)
    {
        foreach (var c in user.FindAll("role")) yield return c.Value;
        foreach (var c in user.FindAll(ClaimTypes.Role)) yield return c.Value;

        var realmAccess = user.FindFirst("realm_access")?.Value;
        if (realmAccess is null) yield break;

        List<string> roles = [];
        try
        {
            using var doc = JsonDocument.Parse(realmAccess);
            if (doc.RootElement.TryGetProperty("roles", out var arr) && arr.ValueKind == JsonValueKind.Array)
                roles.AddRange(arr.EnumerateArray().Select(r => r.GetString()).OfType<string>());
        }
        catch (JsonException) { }

        foreach (var r in roles) yield return r;
    }

    public static bool HasAnyRole(this ClaimsPrincipal user, params string[] roles) =>
        user.GetRoles().Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));
}
