using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Health check підключення: команда ping до бази з MongoDb:DatabaseName.</summary>
public sealed class MongoHealthCheck(MongoDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken ct = default)
    {
        try
        {
            await context.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
            return HealthCheckResult.Healthy($"MongoDB '{context.Database.DatabaseNamespace.DatabaseName}' is reachable");
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            return new HealthCheckResult(healthContext.Registration.FailureStatus, "MongoDB is unreachable", ex);
        }
    }
}
