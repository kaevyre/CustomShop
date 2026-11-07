using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Спільні налаштування платформи: Serilog (структуровані логи → Console + OpenTelemetry),
/// OpenTelemetry (трасування HTTP, SQL Server, MongoDB, метрики, OTLP-експорт у Aspire Dashboard),
/// CorrelationId, health checks, service discovery і resilience для HttpClient.
/// Підключається одним викликом <c>builder.AddServiceDefaults()</c> у кожному сервісі.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>ActivitySource драйвера MongoDB (пакет MongoDB.Driver.Core.Extensions.DiagnosticSources).</summary>
    public const string MongoActivitySource = "MongoDB.Driver.Core.Extensions.DiagnosticSources";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddSerilogLogging();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();                  // retry, timeout, circuit breaker (детально — Лаб. 07)
            http.AddServiceDiscovery();                           // http://orders-api -> реальна адреса від Aspire
            http.AddHttpMessageHandler<CorrelationIdHandler>();   // той самий X-Correlation-Id у вихідних запитах
        });

        return builder;
    }

    /// <summary>
    /// Serilog — основний провайдер логування. Кожен запис — об'єкт з полями (CompactJsonFormatter у консоль),
    /// а writeToProviders: true передає записи далі провайдеру OpenTelemetry → вкладка Structured logs у Dashboard.
    /// </summary>
    public static TBuilder AddSerilogLogging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var env = builder.Environment;

        // стандартний Console-провайдер не потрібен: у консоль пише Serilog, інакше кожен запис був би двічі
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog((services, cfg) => cfg
            .MinimumLevel.Is(env.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
            // фільтрація шумних логів інфраструктури
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
            .MinimumLevel.Override("Polly", LogEventLevel.Warning)
            .MinimumLevel.Override("Yarp", LogEventLevel.Information)
            // сервіс може уточнити рівні у власній секції "Serilog" appsettings.json
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()                                         // CorrelationId з middleware
            .Enrich.WithProperty("ServiceName", ServiceName(builder))
            .Enrich.WithProperty("Environment", env.EnvironmentName)
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new CompactJsonFormatter()),                    // JSON у Console logs Dashboard
            writeToProviders: true);                                         // -> OpenTelemetry -> Structured logs

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var appName = builder.Environment.ApplicationName;          // напр. Orders.Api
        var appPrefix = appName.Split('.')[0];                      // Orders -> власні spans з Orders.Bll тощо

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(
            [
                new("deployment.environment", builder.Environment.EnvironmentName),
                new("host.name", Environment.MachineName)
            ]))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(appName))                                 // власні метрики (напр. Gateway)
            .WithTracing(tracing => tracing
                .SetSampler(CreateSampler(builder))
                .AddSource(appName)                                 // власні spans бізнес-операцій
                .AddSource($"{appPrefix}.*")                        // Orders.Bll, Catalog.Bll, Reviews.Application
                .AddSource(MongoActivitySource)                     // MongoDB (Лаб. 04)
                .AddAspNetCoreInstrumentation(o =>
                    // health checks не засмічують трасування
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments(HealthEndpointPath)
                                   && !ctx.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation());                    // Dapper (Лаб. 02) та EF Core (Лаб. 03)

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    /// <summary>100 % у Development, у решті середовищ — частка з OpenTelemetry:SamplingRatio (за замовчуванням 0.25).</summary>
    private static Sampler CreateSampler(IHostApplicationBuilder builder)
    {
        var ratio = builder.Environment.IsDevelopment()
            ? 1.0
            : builder.Configuration.GetValue("OpenTelemetry:SamplingRatio", 0.25);
        return new ParentBasedSampler(new TraceIdRatioBasedSampler(Math.Clamp(ratio, 0.0, 1.0)));
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // адресу OTLP задає Aspire (OTEL_EXPORTER_OTLP_ENDPOINT); експорт батчевий, тож не блокує запити
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    /// <summary>/health — усі перевірки (готовність, у т.ч. БД); /alive — лише «live» (процес живий).</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });
        return app;
    }

    private static string ServiceName(IHostApplicationBuilder builder) =>
        builder.Configuration["OTEL_SERVICE_NAME"] is { Length: > 0 } name ? name : builder.Environment.ApplicationName;
}
