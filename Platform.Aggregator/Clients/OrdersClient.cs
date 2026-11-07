using System.Diagnostics;
using System.Net;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Typed HttpClient для orders-api. BaseAddress = https+http://orders-api (service discovery);
/// CorrelationId, resilience і трасування додаються до всіх клієнтів у ServiceDefaults.
/// </summary>
public sealed class OrdersClient(HttpClient http, ILogger<OrdersClient> logger)
{
    public const string ServiceName = "orders-api";

    /// <summary>Замовлення з позиціями; null — якщо замовлення не існує (404).</summary>
    public async Task<OrderDto?> GetOrderAsync(long id, CancellationToken ct)
    {
        logger.LogInformation("Calling {Service} for order {OrderId}", ServiceName, id);
        var sw = Stopwatch.StartNew();

        using var response = await http.GetAsync($"/api/orders/{id}", ct);
        logger.LogInformation("{Service} responded {StatusCode} for order {OrderId} in {ElapsedMs} ms",
            ServiceName, (int)response.StatusCode, id, sw.ElapsedMilliseconds);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(ct);
    }
}
