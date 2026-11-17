using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Platform.Aggregator.Resilience;

/// <summary>Логер для callback-ів circuit breaker: ILoggerFactory з'являється після Build(), тож береться з цього holder-а.</summary>
public static class ResilienceLogging
{
    private static ILoggerFactory? _factory;

    public static void Initialize(ILoggerFactory factory) => _factory = factory;

    // категорія не починається з "Polly": Serilog у ServiceDefaults обмежує "Polly" рівнем Warning, а Closed логується як Information
    public static ILogger Create(string service) =>
        (_factory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
            .CreateLogger($"Platform.Aggregator.Resilience.{service}");
}

public static class ResilienceExtensions
{
    /// <summary>
    /// Кастомізує Standard Resilience Handler для одного клієнта. Стандартний обробник уже підключено до всіх клієнтів
    /// через ConfigureHttpClientDefaults (ServiceDefaults), тому спершу він прибирається (RemoveAllResilienceHandlers),
    /// а потім додається власний — обробники не нашаровуються: у pipeline клієнта завжди рівно один resilience handler.
    /// </summary>
    public static IHttpClientBuilder AddAggregatorResilience(this IHttpClientBuilder client, ResilienceProfile profile)
    {
        client.RemoveAllResilienceHandlers();
        client.AddStandardResilienceHandler(o =>
        {
            // Retry: експоненційна затримка з jitter (проти thundering herd); POST/PUT/DELETE не повторюються
            o.Retry.MaxRetryAttempts = profile.MaxRetryAttempts;
            o.Retry.Delay = profile.RetryDelay;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
            o.Retry.UseJitter = true;
            o.Retry.DisableForUnsafeHttpMethods();

            // Таймаути: загальний враховує повтори (інакше повтори не встигнуть виконатися)
            o.AttemptTimeout.Timeout = profile.AttemptTimeout;
            o.TotalRequestTimeout.Timeout = profile.TotalTimeout;

            // Circuit breaker: SamplingDuration >= 2 × AttemptTimeout, інакше валідація налаштувань не пропустить запуск
            o.CircuitBreaker.FailureRatio = profile.FailureRatio;
            o.CircuitBreaker.MinimumThroughput = profile.MinimumThroughput;
            o.CircuitBreaker.SamplingDuration = profile.SamplingDuration;
            o.CircuitBreaker.BreakDuration = profile.BreakDuration;

            // переходи між станами: Open — Error, Half-Open і Closed — Information (повтори й таймаути логує сам Polly)
            o.CircuitBreaker.OnOpened = args =>
            {
                ResilienceLogging.Create(profile.Service).LogError(
                    "Circuit breaker for {Service} opened for {BreakDuration}. Reason: {Reason}",
                    profile.Service, args.BreakDuration, Describe(args.Outcome));
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnHalfOpened = _ =>
            {
                ResilienceLogging.Create(profile.Service).LogInformation(
                    "Circuit breaker for {Service} is half-open: a trial request is allowed", profile.Service);
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnClosed = _ =>
            {
                ResilienceLogging.Create(profile.Service).LogInformation(
                    "Circuit breaker for {Service} closed: the service has recovered", profile.Service);
                return ValueTask.CompletedTask;
            };
        });
        return client;
    }

    /// <summary>
    /// Retry за gRPC-статусами. gRPC повертає Unavailable/DeadlineExceeded у трейлерах відповіді з HTTP 200,
    /// тому HTTP-обробник їх не повторює — для цього використовується вбудована retry-політика gRPC-клієнта.
    /// Повторюються лише тимчасові статуси; усі виклики Aggregator — читання, тобто ідемпотентні.
    /// </summary>
    public static IHttpClientBuilder AddGrpcRetry(this IHttpClientBuilder client, ResilienceProfile profile) =>
        client.ConfigureChannel(channel => channel.ServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = profile.GrpcMaxAttempts,
                        InitialBackoff = TimeSpan.FromMilliseconds(500),
                        MaxBackoff = TimeSpan.FromSeconds(4),
                        BackoffMultiplier = 2,
                        RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted }
                    }
                }
            }
        });

    private static string Describe(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is { } exception
            ? exception.GetType().Name
            : $"HTTP {(int?)outcome.Result?.StatusCode}";
}
