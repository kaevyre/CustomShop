using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Platform.IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile()
    ];

    // Гранульовані дозволи: окремий scope на кожну дію, а не «все або нічого».
    // "orders_api"/"products_api" — «загальні» scopes (як у завданні), решта — fine-grained.
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope("orders_api", "Orders API (all)"),
        new ApiScope("products_api", "Products API (all)"),
        new ApiScope("orders.read", "Read orders"),
        new ApiScope("orders.write", "Create/update orders"),
        new ApiScope("products.read", "Read products"),
        new ApiScope("products.write", "Create/update products")
    ];

    // ApiResource на кожен мікросервіс → його ім'я потрапляє в claim "aud" токена.
    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource("orders_api", "Orders API")
        {
            Scopes = { "orders_api", "orders.read", "orders.write" }
        },
        new ApiResource("products_api", "Products API")
        {
            Scopes = { "products_api", "products.read", "products.write" }
        }
    ];

    public static IEnumerable<Client> Clients =>
    [
        // Postman — Authorization Code (confidential client; PKCE за замовчуванням обов'язковий у GrantTypes.Code)
        new Client
        {
            ClientId = "postman",
            ClientName = "Postman",
            AllowedGrantTypes = GrantTypes.Code,
            ClientSecrets = { new Secret("secret".Sha256()) },
            RedirectUris = { "https://oauth.pstmn.io/v1/callback" },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "orders_api", "products_api",
                "orders.read", "orders.write", "products.read", "products.write"
            },
            AllowOfflineAccess = true,
            RequireConsent = false
        },

        // Aggregator — Client Credentials (service-to-service): токен БЕЗ user context (немає claim "sub" користувача)
        new Client
        {
            ClientId = "aggregator_service",
            ClientName = "Aggregator Service",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("aggregator_secret".Sha256()) },
            AllowedScopes = { "orders_api", "products_api", "orders.read", "products.read" }
        },

        // Swagger UI — public client (SPA) з PKCE, без секрету
        new Client
        {
            ClientId = "swagger",
            ClientName = "Swagger UI",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris =
            {
                "http://localhost:5080/swagger/oauth2-redirect.html",
                "https://localhost:5001/swagger/oauth2-redirect.html"
            },
            AllowedCorsOrigins = { "http://localhost:5080", "https://localhost:5001" },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "orders_api", "orders.read", "orders.write"
            },
            RequireConsent = false
        }
    ];
}
