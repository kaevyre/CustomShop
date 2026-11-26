using Microsoft.AspNetCore.Authorization;

namespace Platform.ServiceDefaults.Security;

/// <summary>
/// ЛОКАЛЬНА перевірка permission: рішення «пропустити чи відхилити» приймає мікросервіс на рівні endpoint,
/// читаючи claim <c>authorization.permissions</c> з JWT, — без запитів до Keycloak.
/// Немає permission → context не Succeed → ASP.NET Core повертає 403 (не 500).
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (PermissionClaims.GetPermissions(context.User).Contains(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
