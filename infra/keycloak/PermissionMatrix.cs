using System.Text.Json;

namespace Keycloak.Iac;

/// <summary>
/// ЄДИНЕ джерело правди для таблиць 6 і 7 завдання: які scopes має кожен Resource Server
/// і які з них отримує кожна realm-роль. З цієї матриці Pulumi будує Resource Servers, Permission Policies
/// та claim <c>authorization.permissions</c> для токена користувача.
/// </summary>
public static class PermissionMatrix
{
    // Таблиця 6 — Resource Servers і повний набір scopes
    public static readonly IReadOnlyDictionary<string, string[]> ResourceServers = new Dictionary<string, string[]>
    {
        ["orders-api"] = ["orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve"],
        ["products-api"] = ["products:read", "products:create", "products:update", "products:delete", "products:publish"],
        ["payments-api"] = ["payments:read", "payments:process", "payments:refund"]
    };

    public static readonly string[] Roles = ["admin", "manager", "user", "guest"];

    // Таблиця 7 — що отримує кожна роль на кожному Resource Server. payments-api — ЛИШЕ admin (свідоме рішення).
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string[]>> RoleGrants =
        new Dictionary<string, IReadOnlyDictionary<string, string[]>>
        {
            ["admin"] = ResourceServers.ToDictionary(kv => kv.Key, kv => kv.Value),
            ["manager"] = new Dictionary<string, string[]>
            {
                ["orders-api"] = ["orders:read", "orders:update", "orders:approve"],
                ["products-api"] = ["products:read", "products:update", "products:publish"]
            },
            ["user"] = new Dictionary<string, string[]>
            {
                ["orders-api"] = ["orders:read", "orders:create"],
                ["products-api"] = ["products:read"]
            },
            ["guest"] = new Dictionary<string, string[]>
            {
                ["products-api"] = ["products:read"]
            }
        };

    /// <summary>Scopes, які роль отримує на конкретному Resource Server (порожньо — якщо доступу немає).</summary>
    public static string[] Granted(string role, string resourceServer) =>
        RoleGrants[role].TryGetValue(resourceServer, out var s) ? s : [];

    /// <summary>
    /// JSON для claim "authorization" (union permissions усіх ролей користувача):
    /// {"permissions":[{"rsname":"orders-api","scopes":["orders:read",...]}, ...]}
    /// </summary>
    public static string BuildAuthorizationJson(IEnumerable<string> roles)
    {
        var roleList = roles.ToList();
        var permissions = ResourceServers.Keys
            .Select(rs => new
            {
                rsname = rs,
                scopes = roleList.SelectMany(r => Granted(r, rs)).Distinct().ToArray()
            })
            .Where(p => p.scopes.Length > 0)
            .ToArray();

        return JsonSerializer.Serialize(new { permissions });
    }
}

/// <summary>Тестові користувачі — по одному на кожну роль.</summary>
public record TestUser(string Username, string Email, string FirstName, string LastName, string Role);

public static class TestUsers
{
    public static readonly TestUser[] All =
    [
        new("alice.admin", "alice.admin@platform.local", "Alice", "Admin", "admin"),
        new("john.manager", "john.manager@platform.local", "John", "Manager", "manager"),
        new("kate.user", "kate.user@platform.local", "Kate", "User", "user"),
        new("greg.guest", "greg.guest@platform.local", "Greg", "Guest", "guest")
    ];
}
