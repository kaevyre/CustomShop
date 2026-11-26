using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Platform.Aggregator.Auth;

public class ClientCredentialsOptions
{
    public string Authority { get; set; } = "http://localhost:5000";
    public string ClientId { get; set; } = "aggregator_service";
    public string ClientSecret { get; set; } = "aggregator_secret";
    public string Scope { get; set; } = "orders.read products.read";
}

/// <summary>
/// Client Credentials Flow: POST /connect/token (grant_type=client_credentials). Токен містить лише ідентичність
/// сервісу (БЕЗ user context — немає sub користувача); кешується до закінчення терміну дії.
/// </summary>
public sealed class ClientCredentialsTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ClientCredentialsOptions> options,
    ILogger<ClientCredentialsTokenProvider> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTime _expiresAt = DateTime.MinValue;

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (_token is not null && DateTime.UtcNow < _expiresAt) return _token;

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _expiresAt) return _token;

            var o = options.Value;
            using var http = httpClientFactory.CreateClient("token");
            using var response = await http.PostAsync($"{o.Authority.TrimEnd('/')}/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = o.ClientId,
                    ["client_secret"] = o.ClientSecret,
                    ["scope"] = o.Scope
                }), ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                       ?? throw new InvalidOperationException("Empty token response");
            _token = body.AccessToken;
            _expiresAt = DateTime.UtcNow.AddSeconds(body.ExpiresIn - 60); // запас 60 с
            logger.LogInformation("Отримано токен Client Credentials (expires_in={ExpiresIn}s)", body.ExpiresIn);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>Додає "Authorization: Bearer ..." до вихідних запитів Aggregator → Orders/Products.</summary>
public sealed class ClientCredentialsHandler(ClientCredentialsTokenProvider tokens, Microsoft.Extensions.Configuration.IConfiguration config) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
        if (config["Gateway:Secret"] is { Length: > 0 } gatewaySecret)       // Aggregator → сервіси: дозволений шлях повз Gateway
            request.Headers.TryAddWithoutValidation(Platform.ServiceDefaults.Security.GatewayOnlyExtensions.HeaderName, gatewaySecret);
        return await base.SendAsync(request, ct);
    }
}
