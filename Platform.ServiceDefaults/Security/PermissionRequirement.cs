using Microsoft.AspNetCore.Authorization;

namespace Platform.ServiceDefaults.Security;

/// <summary>Вимога «у токені є permission X» (наприклад, "orders:delete").</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
