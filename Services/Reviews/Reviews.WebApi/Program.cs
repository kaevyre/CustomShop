using Microsoft.Extensions.Diagnostics.HealthChecks;
using Reviews.Application;
using Reviews.Infrastructure;
using Reviews.Infrastructure.Persistence;
using Reviews.WebApi;
using Reviews.WebApi.Grpc;
using Reviews.WebApi.Metrics;

var builder = WebApplication.CreateBuilder(args);
// Лаб. 05: Serilog (рівні — із секції "Serilog" appsettings.json), OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// Лаб. 06: L1 — кеш у пам'яті з обмеженням розміру, L2 — Redis через Aspire-інтеграцію (ConnectionStrings:redis)
builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;                 // в одиницях Size записів
    o.CompactionPercentage = 0.25;
    o.TrackStatistics = true;
});
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true,
        o => { o.AbortOnConnectFail = false; o.ConnectTimeout = 2000; o.AsyncTimeout = 1000; o.SyncTimeout = 1000; });
else
    builder.Services.AddDistributedMemoryCache();   // запуск без Aspire
builder.AddCacheHealthCheck();                   // Лаб. 07: redis-cache — Degraded, а не Unhealthy (кеш необов'язковий)

// Лаб. 07: бізнес-метрики (Meter "Reviews.Api" через IMeterFactory; Singleton)
builder.Services.AddSingleton<ReviewMetrics>();

builder.Services.AddGrpc(o =>
{
    o.Interceptors.Add<GrpcLoggingInterceptor>();
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// кожен шар реєструє свої залежності власним методом розширення
builder.Services
    .AddApplication(builder.Configuration)       // MediatR + behaviors, валідатори, AutoMapper
    .AddInfrastructure(builder.Configuration)    // MongoDB: контекст, BsonClassMap, репозиторії, індекси, сідінг
    .AddWebApi();                                // контролери, Swagger, ProblemDetails

// Лаб. 07: health check MongoDB (команда ping) — ім'я й теги для Dashboard і /health/ready, таймаут 5 с
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>(
        name: "reviews-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "mongodb", "ready"],
        timeout: TimeSpan.FromSeconds(5));

// Лаб. 07: стандартний gRPC Health Checking Protocol; статус сервісу "" залежить лише від перевірок бази (Redis — Degraded)
builder.Services.AddGrpcHealthChecks(o =>
    o.Services.Map("", check => check.Tags.Contains("database")));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();  // X-Correlation-Id → LogContext і заголовок відповіді
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapDefaultEndpoints();                     // /health, /alive, /health/live, /health/ready (включно з перевіркою MongoDB), /health/startup
app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();      // endpoint "grpc" (HTTP/2) із Kestrel:Endpoints
app.MapGrpcHealthChecksService();              // grpc.health.v1.Health: Check/Watch для Aggregator

app.Run();

public partial class Program;
