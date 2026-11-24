using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.ServiceDefaults.Security;

public static class PermissionServiceCollectionExtensions
{
    /// <summary>Реєструє handler і політику на КОЖЕН permission сервісу (таблиці 3.3.1–3.3.3).</summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services, params string[] permissions)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            foreach (var permission in permissions)
                options.AddPolicy(permission, p => p.Requirements.Add(new PermissionRequirement(permission)));
        });
        return services;
    }
}
