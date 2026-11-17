using System.Diagnostics;
using Catalog.Bll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

/// <summary>Прогрівання кешу на старті: довідники, які запитують найчастіше, доступні з першого запиту.</summary>
public sealed class CacheWarmupService(IServiceScopeFactory scopes, ILogger<CacheWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var categories = await scope.ServiceProvider.GetRequiredService<ICategoryService>().GetAllAsync(stoppingToken);
            var brands = await scope.ServiceProvider.GetRequiredService<IBrandService>().GetAllAsync(stoppingToken);

            logger.LogInformation("Cache warmed up: {CategoriesCount} categories, {BrandsCount} brands in {ElapsedMs} ms",
                categories.Count, brands.Count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // прогрівання необов'язкове: дані потраплять у кеш за першим запитом
            logger.LogWarning(ex, "Cache warm-up failed after {ElapsedMs} ms", sw.ElapsedMilliseconds);
        }
    }
}
