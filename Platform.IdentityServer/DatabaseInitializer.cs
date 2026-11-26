using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Platform.IdentityServer.Data;
using Platform.IdentityServer.Models;

namespace Platform.IdentityServer;

/// <summary>
/// Створює схему трьох контекстів (PersistedGrant, Configuration, Application) і засіває clients/scopes/користувачів.
/// Якщо в проєкті є migrations (dotnet ef migrations add ... -c &lt;Context&gt;) — застосовує їх,
/// інакше створює таблиці напряму (щоб проєкт стартував «з коробки»).
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        await EnsureSchemaAsync(sp.GetRequiredService<PersistedGrantDbContext>());
        var config = sp.GetRequiredService<ConfigurationDbContext>();
        await EnsureSchemaAsync(config);
        await EnsureSchemaAsync(sp.GetRequiredService<ApplicationDbContext>());

        await SeedConfigurationAsync(config);
        await SeedUsersAsync(sp.GetRequiredService<UserManager<ApplicationUser>>());
    }

    private static async Task EnsureSchemaAsync(DbContext db)
    {
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
            return;
        }

        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync()) await creator.CreateAsync();
        try
        {
            await creator.CreateTablesAsync();
        }
        catch (SqlException ex) when (ex.Number == 2714) // таблиці вже існують
        {
        }
    }

    private static async Task SeedConfigurationAsync(ConfigurationDbContext db)
    {
        if (!await db.IdentityResources.AnyAsync())
            db.IdentityResources.AddRange(Config.IdentityResources.Select(r => r.ToEntity()));
        if (!await db.ApiScopes.AnyAsync())
            db.ApiScopes.AddRange(Config.ApiScopes.Select(s => s.ToEntity()));
        if (!await db.ApiResources.AnyAsync())
            db.ApiResources.AddRange(Config.ApiResources.Select(r => r.ToEntity()));
        if (!await db.Clients.AnyAsync())
            db.Clients.AddRange(Config.Clients.Select(c => c.ToEntity()));

        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> users)
    {
        foreach (var (name, email) in new[] { ("alice", "alice@platform.local"), ("bob", "bob@platform.local") })
        {
            if (await users.FindByNameAsync(name) is not null) continue;
            var user = new ApplicationUser { UserName = name, Email = email, EmailConfirmed = true };
            await users.CreateAsync(user, "Pass123$");
        }
    }
}
