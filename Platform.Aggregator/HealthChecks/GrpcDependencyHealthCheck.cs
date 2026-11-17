using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Перевіряє gRPC-залежність Aggregator через стандартний gRPC Health Checking Protocol (метод Check клієнта Health).
/// Короткий дедлайн (2 с) — для швидкого виявлення недоступності; RpcException перетворюється на Unhealthy, збій логується.
/// Перевірка ідемпотентна й не має побічних ефектів у сервісі, який перевіряє.
/// </summary>
public sealed class GrpcDependencyHealthCheck(
    GrpcClientFactory clientFactory, string clientName, string service, ILogger<GrpcDependencyHealthCheck> logger) : IHealthCheck
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = clientFactory.CreateClient<Health.HealthClient>(clientName);
            var reply = await client.CheckAsync(new HealthCheckRequest(),
                deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: cancellationToken);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{service} is serving")
                : new HealthCheckResult(context.Registration.FailureStatus, $"{service} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "{Service} health check failed with gRPC status {StatusCode}", service, ex.StatusCode);
            return new HealthCheckResult(context.Registration.FailureStatus, $"{service} is unavailable", ex);
        }
    }
}

public static class GrpcDependencyHealthCheckExtensions
{
    /// <summary>
    /// Реєструє іменований gRPC-клієнт Health для сервісу й health check "&lt;service&gt;" з тегами grpc/downstream/ready.
    /// Клієнт Health без повторів (лише RemoveAllResilienceHandlers): health check має відповідати одразу, а не після серії спроб.
    /// </summary>
    public static IServiceCollection AddGrpcDependencyHealthCheck(this IServiceCollection services, string service, string address)
    {
        var clientName = $"{service}-health";
        services.AddGrpcClient<Health.HealthClient>(clientName, o => o.Address = new Uri(address))
            .RemoveAllResilienceHandlers();

        services.AddHealthChecks().Add(new HealthCheckRegistration(
            name: service,
            factory: sp => new GrpcDependencyHealthCheck(
                sp.GetRequiredService<GrpcClientFactory>(), clientName, service,
                sp.GetRequiredService<ILogger<GrpcDependencyHealthCheck>>()),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["grpc", "downstream", "ready"],
            timeout: TimeSpan.FromSeconds(3)));
        return services;
    }
}
