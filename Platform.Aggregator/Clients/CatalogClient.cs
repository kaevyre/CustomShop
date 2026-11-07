using System.Diagnostics;
using System.Net;

namespace Platform.Aggregator.Clients;

/// <summary>Typed HttpClient для catalog-api (BaseAddress = https+http://catalog-api).</summary>
public sealed class CatalogClient(HttpClient http, ILogger<CatalogClient> logger)
{
    public const string ServiceName = "catalog-api";

    /// <summary>Актуальна картка товару; null — якщо товару в каталозі вже немає (404).</summary>
    public async Task<ProductDto?> GetProductAsync(long id, CancellationToken ct)
    {
        logger.LogInformation("Calling {Service} for product {ProductId}", ServiceName, id);
        var sw = Stopwatch.StartNew();

        using var response = await http.GetAsync($"/api/products/{id}", ct);
        logger.LogInformation("{Service} responded {StatusCode} for product {ProductId} in {ElapsedMs} ms",
            ServiceName, (int)response.StatusCode, id, sw.ElapsedMilliseconds);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(ct);
    }

    /// <summary>Кілька товарів паралельно (у catalog-api немає пакетного ендпоінта); відсутні товари пропускаються.</summary>
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var products = await Task.WhenAll(ids.Select(id => GetProductAsync(id, ct)));
        return products.OfType<ProductDto>().ToList();
    }
}
