using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.IdentityServer;
using Platform.IdentityServer.Data;
using Platform.IdentityServer.Models;

var builder = WebApplication.CreateBuilder(args);

// Окрема БД "IdentityServerDb" (WithReference в AppHost) — ізоляція від бізнес-мікросервісів
var connectionString = builder.Configuration.GetConnectionString("IdentityServerDb")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityServerDb is not configured");
var migrationsAssembly = typeof(Program).GetTypeInfo().Assembly.GetName().Name;

builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddRazorPages();

builder.Services.AddIdentityServer(options =>
    {
        // Фіксований issuer = Authority, яку очікують мікросервіси (IdentityServer__Authority в AppHost)
        options.IssuerUri = builder.Configuration["IdentityServer:IssuerUri"] ?? "http://localhost:5300";
        options.EmitStaticAudienceClaim = false;      // aud беремо з ApiResources, а не статичний
        var licenseKey = builder.Configuration["IdentityServer:LicenseKey"];
        if (!string.IsNullOrWhiteSpace(licenseKey)) options.LicenseKey = licenseKey; // Community Edition; без ключа — dev-режим
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseFailureEvents = true;
    })
    .AddAspNetIdentity<ApplicationUser>()
    .AddConfigurationStore(options =>
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly)))
    .AddOperationalStore(options =>
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly)));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseStaticFiles();
app.UseCors();
app.UseRouting();
app.UseIdentityServer();   // включає UseAuthentication
app.UseAuthorization();
app.MapRazorPages();

app.Run();
