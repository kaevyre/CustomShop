using Catalog.Dal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.Api.HealthChecks;

/// <summary>
/// Health check SQL Server (база каталогу): найпростіший запит SELECT 1 через той самий DbContext.
/// Легка перевірка без звернення до таблиць; таймаут (5 с) задається при реєстрації в Program.cs.
/// </summary>
public sealed class CatalogDbHealthCheck(CatalogDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("SQL Server (catalog-db) is reachable");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "SQL Server (catalog-db) is unreachable", ex);
        }
    }
}
