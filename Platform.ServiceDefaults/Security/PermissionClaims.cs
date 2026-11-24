using System.Security.Claims;
using System.Text.Json;

namespace Platform.ServiceDefaults.Security;

/// <summary>
/// Витягує permissions із токена БЕЗ звернення до провайдера ідентичності.
/// Джерела:
///  1) Keycloak — claim "authorization": {"permissions":[{"rsname":"orders-api","scopes":["orders:read",...]}]};
///  2) IdentityServer — claim "scope" (напр. "orders.read"); нормалізується до "orders:read", тож один і той самий
///     [RequirePermission("orders:read")] працює для обох провайдерів.
/// </summary>
public static class PermissionClaims
{
    public const string AuthorizationClaim = "authorization";

    public static HashSet<string> GetPermissions(ClaimsPrincipal user)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in user.FindAll(AuthorizationClaim))
        {
            try
            {
                using var doc = JsonDocument.Parse(claim.Value);
                if (!doc.RootElement.TryGetProperty("permissions", out var permissions) ||
                    permissions.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var resource in permissions.EnumerateArray())
                {
                    if (!resource.TryGetProperty("scopes", out var scopes) || scopes.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var scope in scopes.EnumerateArray())
                        if (scope.GetString() is { Length: > 0 } s) result.Add(s);
                }
            }
            catch (JsonException)
            {
                // некоректний claim — ігноруємо, permission не надається
            }
        }

        // Keycloak віддає "scope" одним рядком через пробіл, IdentityServer — окремими claim'ами
        foreach (var claim in user.FindAll("scope"))
            foreach (var s in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(s.Replace('.', ':'));

                // coarse-grained scope IdentityServer "<resource>.write" покриває create + update
                // (delete/approve потребують окремих permissions — їх IdentityServer-клієнти не отримують)
                if (s.EndsWith(".write", StringComparison.Ordinal))
                {
                    var resource = s[..^".write".Length];
                    result.Add($"{resource}:create");
                    result.Add($"{resource}:update");
                }
            }

        return result;
    }
}
