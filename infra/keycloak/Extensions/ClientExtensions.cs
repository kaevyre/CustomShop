using System.Collections.Immutable;
using Pulumi;
using Pulumi.Keycloak.OpenId;

namespace Keycloak.Iac.Extensions;

/// <summary>Extension methods: default scopes та audience mappers клієнтів.</summary>
public static class ClientExtensions
{
    private static readonly string[] BaselineScopes = ["profile", "email", "roles", "web-origins", "basic", "acr"];

    /// <summary>Default scopes клієнта: стандартні + власні (orders_mgmt, permissions, ...).</summary>
    public static Client WithDefaultScopes(this Client client, Output<string> realmId, string resourceName,
        IEnumerable<Output<string>> extraScopeNames, CustomResourceOptions options)
    {
        _ = new ClientDefaultScopes($"{resourceName}-default-scopes", new ClientDefaultScopesArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            DefaultScopes = Output.All(extraScopeNames).Apply(extra => BaselineScopes.Concat(extra).ToImmutableArray())
        }, options);
        return client;
    }

    /// <summary>
    /// Audience mapper: додає ім'я Resource Server ("orders-api") в claim "aud" access token —
    /// мікросервіс перевіряє саме свій audience.
    /// </summary>
    public static Client WithAudience(this Client client, Output<string> realmId, string resourceName,
        string audience, CustomResourceOptions options)
    {
        _ = new AudienceProtocolMapper($"{resourceName}-aud-{audience}", new AudienceProtocolMapperArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            Name = $"audience-{audience}",
            IncludedCustomAudience = audience,
            AddToAccessToken = true,
            AddToIdToken = false
        }, options);
        return client;
    }
}
