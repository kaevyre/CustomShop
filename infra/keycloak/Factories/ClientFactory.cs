using Keycloak.Iac.Extensions;
using Pulumi;
using Pulumi.Keycloak.OpenId;

namespace Keycloak.Iac.Factories;

/// <summary>
/// Factory pattern: окремі методи для кожного flow. Усі клієнти отримують default scopes та audience mappers
/// для трьох Resource Servers через extension methods.
/// </summary>
public sealed class ClientFactory(Output<string> realmId, CustomResourceOptions options,
    IReadOnlyList<Output<string>> defaultScopeNames)
{
    private static readonly string[] AllAudiences = ["orders-api", "products-api", "payments-api"];

    /// <summary>Authorization Code Flow: Postman (confidential), Swagger (public + PKCE), BFF (confidential).</summary>
    public Client CreateAuthorizationCodeClient(string clientId, string name, string[] redirectUris,
        string[] webOrigins, bool isPublic)
    {
        var client = new Client($"client-{clientId}", new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = name,
            Enabled = true,
            AccessType = isPublic ? "PUBLIC" : "CONFIDENTIAL",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = !isPublic,   // DEV/тести: Password grant для скриптової перевірки матриці; Postman далі працює через Authorization Code
            ServiceAccountsEnabled = false,
            ValidRedirectUris = redirectUris,
            WebOrigins = webOrigins,
            // public client (SPA, Swagger UI) — PKCE обов'язковий
            PkceCodeChallengeMethod = isPublic ? "S256" : null,
            FullScopeAllowed = true
        }, options);

        return Decorate(client, clientId);
    }

    /// <summary>Client Credentials Flow (service-to-service): Aggregator. Токен — без user context.</summary>
    public Client CreateClientCredentialsClient(string clientId, string name)
    {
        var client = new Client($"client-{clientId}", new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = name,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            ServiceAccountsEnabled = true
        }, options);

        return Decorate(client, clientId);
    }

    private Client Decorate(Client client, string resourceName)
    {
        client.WithDefaultScopes(realmId, resourceName, defaultScopeNames, options);
        foreach (var audience in AllAudiences)
            client.WithAudience(realmId, resourceName, audience, options);
        return client;
    }
}
