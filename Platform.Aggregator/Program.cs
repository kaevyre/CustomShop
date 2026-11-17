using System.Text.Json;
using System.Text.Json.Serialization;
using Catalog.Grpc;
using Microsoft.Extensions.Http.Resilience;
using Orders.Grpc;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Reviews.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();          // Serilog, OpenTelemetry, service discovery, resilience, CorrelationIdHandler

// Типізовані gRPC-клієнти (Лаб. 06): "_grpc" — іменований endpoint сервісу лише з HTTP/2, адресу підставляє service discovery.
// Клієнти створює та сама фабрика HttpClient, тож ServiceDefaults додає resilience і X-Correlation-Id у метадані викликів.
builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = new Uri("http://_grpc.orders-api"));
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("http://_grpc.catalog-api"));
builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri("http://_grpc.reviews-api"));

// Агреговані відповіді кешуються в Redis (ConnectionStrings:redis від Aspire)
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true,
        o => { o.AbortOnConnectFail = false; o.ConnectTimeout = 2000; o.AsyncTimeout = 1000; o.SyncTimeout = 1000; });
else
    builder.Services.AddDistributedMemoryCache();   // запуск без Aspire
builder.Services.AddSingleton<AggregatorCacheMetrics>();
builder.Services.AddSingleton<OrderCardCache>();

// Typed HttpClients (Лаб. 05) лишаються для порівняння затримок HTTP і gRPC: /api/aggregator/orders/{id}/full/http
builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));

// Агрегатор має відповідати швидко навіть коли сервіс лежить: коротші тайм-аути й менше повторів,
// ніж у стандартному resilience handler з ServiceDefaults (детально — Лаб. 07)
builder.Services.ConfigureAll<HttpStandardResilienceOptions>(o =>
{
    o.Retry.MaxRetryAttempts = 2;
    o.Retry.Delay = TimeSpan.FromMilliseconds(200);
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<OrderCardService>();
builder.Services.AddScoped<HttpOrderCardService>();

// JSON у camelCase, null-значення не серіалізуються
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

app.MapOpenApi();                      // /openapi/v1.json
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
