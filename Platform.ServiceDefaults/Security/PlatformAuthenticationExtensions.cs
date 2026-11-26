using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Platform.ServiceDefaults.Security;

/// <summary>Очікувані audience для сервісу в кожного провайдера (null у Gateway — audience не перевіряється).</summary>
public sealed record ServiceAudiences(string Keycloak, string IdentityServer);

public static class PlatformAuthenticationExtensions
{
    public const string Smart = "Smart";
    public const string Local = "Local";                 // Частина 1: Auth Service (ASP.NET Identity + HMAC-JWT)
    public const string IdentityServer = "IdentityServer"; // Частина 2: Duende (Authority + discovery)
    public const string Keycloak = "Keycloak";           // Частина 3: Keycloak (Aspire service discovery)

    /// <summary>Лише для локальної розробки: той самий ключ, що в appsettings Auth.Api. У production — Jwt__SecretKey із секретів.</summary>
    private const string DevSecretKey = "dev-only-super-secret-key-change-me-0123456789-abcdef";

    /// <summary>
    /// Мікросервіс/Gateway валідує JWT НЕЗАЛЕЖНО (підпис, issuer, audience, lifetime) і НЕ звертається до
    /// провайдера на кожен запит: ключі підтягуються/кешуються з discovery document.
    /// Схема обирається за claim "iss" токена, тож одна й та сама інфраструктура працює з усіма трьома підходами.
    /// </summary>
    public static AuthenticationBuilder AddPlatformAuthentication(
        this WebApplicationBuilder builder, ServiceAudiences? audiences = null)
    {
        var cfg = builder.Configuration;
        var isDev = builder.Environment.IsDevelopment();

        var localIssuer = cfg["Jwt:Issuer"] ?? "auth-service";
        var localAudience = cfg["Jwt:Audience"] ?? "microservices-platform";
        var localKey = cfg["Jwt:SecretKey"]
                       ?? (isDev ? DevSecretKey : throw new InvalidOperationException("Jwt:SecretKey is not configured"));
        var idsAuthority = cfg["IdentityServer:Authority"];
        var kcRealm = cfg["Keycloak:Realm"] ?? "DevRealmDrivers";
        var kcPublicUrl = (cfg["Keycloak:PublicUrl"] ?? "http://localhost:8080").TrimEnd('/');
        var validateAudience = audiences is not null;

        var auth = builder.Services
            .AddAuthentication(o =>
            {
                o.DefaultScheme = Smart;
                o.DefaultChallengeScheme = Smart;
            })
            .AddPolicyScheme(Smart, "Select scheme by token issuer",
                o => o.ForwardDefaultSelector = ctx => SelectScheme(ctx, localIssuer, idsAuthority));

        // ── Частина 1: Auth Service (symmetric key, ті самі issuer/secret, що й в Auth.Api)
        auth.AddJwtBearer(Local, o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = localIssuer,
                ValidAudience = localAudience,
                ValidateAudience = validateAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(localKey)),
                RoleClaimType = "role",
                NameClaimType = "unique_name",
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

        // ── Частина 2: Duende IdentityServer — signing keys з /.well-known/openid-configuration
        if (!string.IsNullOrWhiteSpace(idsAuthority))
        {
            auth.AddJwtBearer(IdentityServer, o =>
            {
                o.Authority = idsAuthority;
                o.RequireHttpsMetadata = !isDev;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = validateAudience,
                    ValidAudience = audiences?.IdentityServer,
                    RoleClaimType = "role",
                    NameClaimType = "name"
                };
            });
        }

        // ── Частина 3: Keycloak — Aspire знаходить Keycloak через service discovery
        auth.AddKeycloakJwtBearer(serviceName: "keycloak", realm: kcRealm, authenticationScheme: Keycloak, o =>
        {
            o.MapInboundClaims = false;
            o.RequireHttpsMetadata = !isDev;           // Keycloak у розробці — HTTP
            o.TokenValidationParameters.ValidateAudience = validateAudience;
            if (audiences is not null) o.TokenValidationParameters.ValidAudience = audiences.Keycloak;
            // токени видаються на публічному URL, а метадані тягнуться по service discovery
            o.TokenValidationParameters.ValidIssuers = [$"{kcPublicUrl}/realms/{kcRealm}"];
            o.TokenValidationParameters.RoleClaimType = "role";
        });

        return auth;
    }

    private static string SelectScheme(HttpContext ctx, string localIssuer, string? idsAuthority)
    {
        var header = ctx.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Local;

        var token = header["Bearer ".Length..].Trim();
        var handler = new JsonWebTokenHandler();
        if (!handler.CanReadToken(token)) return Local;

        var issuer = handler.ReadJsonWebToken(token).Issuer?.TrimEnd('/');
        if (issuer is null || issuer == localIssuer) return Local;
        if (idsAuthority is not null && issuer == idsAuthority.TrimEnd('/')) return IdentityServer;
        if (issuer.Contains("/realms/", StringComparison.Ordinal)) return Keycloak;
        return Local;
    }
}
