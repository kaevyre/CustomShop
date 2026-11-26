using Microsoft.AspNetCore.Authorization;

namespace Platform.ServiceDefaults.Security;

/// <summary>
/// [RequirePermission("orders:create")] — політика з іменем permission (реєструється в Program.cs через
/// <see cref="PermissionServiceCollectionExtensions.AddPermissionAuthorization"/>).
/// Неавтентифікований запит → 401, автентифікований без permission → 403.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(permission)
{
    public string Permission { get; } = permission;
}
