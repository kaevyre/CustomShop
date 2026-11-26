using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Data;

public static class IdentitySeeder
{
    public static readonly string[] Roles = ["User", "Admin"];

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Є migrations (dotnet ef migrations add) → застосовуємо; ні — створюємо схему з моделі.
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // Тестовий адміністратор — у Development або коли явно ввімкнено Seed:Enabled=true (лабораторна; у production вимкнути).
        if (!isDevelopment && !config.GetValue<bool>("Seed:Enabled")) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
        var email = config["Seed:AdminEmail"] ?? "admin@platform.local";
        var password = config["Seed:AdminPassword"] ?? "Admin123!";

        var admin = await users.FindByNameAsync("admin") ?? await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = "admin", Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(admin, password);
            if (!created.Succeeded)
            {
                logger.LogError("Seed admin NOT created: {Errors}", string.Join("; ", created.Errors.Select(e => $"{e.Code}: {e.Description}")));
                return;
            }
            logger.LogInformation("Seed admin created (username 'admin')");
        }
        else
        {
            // існуючий запис: гарантуємо відомий пароль, підтверджений email і lockout без блокування (dev-середовище)
            admin.EmailConfirmed = true;
            await users.UpdateAsync(admin);
            await users.ResetAccessFailedCountAsync(admin);
            await users.SetLockoutEndDateAsync(admin, null);
            var reset = await users.ResetPasswordAsync(admin, await users.GeneratePasswordResetTokenAsync(admin), password);
            logger.LogInformation("Seed admin updated, password reset succeeded: {Ok}", reset.Succeeded);
        }

        var current = await users.GetRolesAsync(admin);
        var missing = Roles.Except(current).ToArray();
        if (missing.Length > 0) await users.AddToRolesAsync(admin, missing);
    }
}
