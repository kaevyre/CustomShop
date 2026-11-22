using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orders.Api.HealthChecks;

/// <summary>
/// Health check SQL Server (база замовлень): найпростіший запит SELECT 1 на окремому короткоживучому з'єднанні.
/// Перевірка виконується регулярно, тож вона навмисно легка; таймаут (5 с) задається при реєстрації в Program.cs.
/// </summary>
public sealed class OrdersDbHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(configuration.GetConnectionString("OrdersDb"));
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("SQL Server (orders-db) is reachable");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // failureStatus береться з реєстрації (Unhealthy): без бази замовлень сервіс працювати не може
            return new HealthCheckResult(context.Registration.FailureStatus, "SQL Server (orders-db) is unreachable", ex);
        }
    }
}
