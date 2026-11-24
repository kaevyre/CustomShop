using System.Text.Json;
using System.Text.Json.Serialization;
using Catalog.Grpc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orders.Grpc;
using Platform.Aggregator.Auth;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults.Security;
using Reviews.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();          // Serilog, OpenTelemetry, service discovery, resilience, CorrelationIdHandler

// Типізовані gRPC-клієнти (Лаб. 06): "_grpc" — іменований endpoint сервісу лише з HTTP/2, адресу підставляє service discovery.
// Клієнти створює та сама фабрика HttpClient, тож ServiceDefaults додає resilience і X-Correlation-Id у метадані викликів.
//
// Лаб. 07: політики resilience різні для критичних (Каталог, Замовлення) і некритичного (Відгуки) сервісів — див. ResilienceProfiles
// і таблицю обґрунтування в README.md. AddAggregatorResilience спершу прибирає стандартний обробник з ServiceDefaults
// (RemoveAllResilienceHandlers), тож обробники не нашаровуються. AddGrpcRetry додає retry за gRPC-статусами
// (Unavailable, ResourceExhausted): HTTP-обробник їх не бачить, бо gRPC повертає їх у трейлерах відповіді з HTTP 200.
//
// Лаб. 09: service-to-service — Client Credentials Flow через IdentityServer (client "aggregator_service"): ClientCredentialsHandler
// додає "Authorization: Bearer ..." до кожного вихідного виклику (токен без user context, кешується до закінчення терміну дії).
builder.Services.Configure<ClientCredentialsOptions>(builder.Configuration.GetSection("IdentityServer:Aggregator"));
builder.Services.Configure<ClientCredentialsOptions>(o =>
    o.Authority = builder.Configuration["IdentityServer:Authority"] ?? o.Authority);
builder.Services.AddHttpClient("token");
builder.Services.AddSingleton<ClientCredentialsTokenProvider>();
builder.Services.AddTransient<ClientCredentialsHandler>();
builder.AddPlatformAuthentication();          // вхідні запити від Gateway: JWT користувача (Auth Service / IdentityServer / Keycloak)
builder.Services.AddAuthorization();

builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = new Uri("http://_grpc.orders-api"))
    .AddAggregatorResilience(ResilienceProfiles.Orders)
    .AddGrpcRetry(ResilienceProfiles.Orders)
    .AddHttpMessageHandler<ClientCredentialsHandler>();
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("http://_grpc.catalog-api"))
    .AddAggregatorResilience(ResilienceProfiles.Catalog)
    .AddGrpcRetry(ResilienceProfiles.Catalog)
    .AddHttpMessageHandler<ClientCredentialsHandler>();
builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri("http://_grpc.reviews-api"))
    .AddAggregatorResilience(ResilienceProfiles.Reviews)
    .AddGrpcRetry(ResilienceProfiles.Reviews)
    .AddHttpMessageHandler<ClientCredentialsHandler>();

// Агреговані відповіді кешуються в Redis (ConnectionStrings:redis від Aspire)
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis", s => s.DisableHealthChecks = true,
        o => { o.AbortOnConnectFail = false; o.ConnectTimeout = 2000; o.AsyncTimeout = 1000; o.SyncTimeout = 1000; });
else
    builder.Services.AddDistributedMemoryCache();   // запуск без Aspire
builder.Services.AddSingleton<AggregatorCacheMetrics>();
builder.Services.AddSingleton<OrderCardCache>();

// Typed HttpClients (Лаб. 05) лишаються для порівняння затримок HTTP і gRPC: /api/aggregator/orders/{id}/full/http
// Ті самі політики, що й для gRPC-клієнтів тих самих сервісів: критичні — Замовлення й Каталог, некритичний — Відгуки.
builder.Services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"))
    .AddAggregatorResilience(ResilienceProfiles.Orders)
    .AddHttpMessageHandler<ClientCredentialsHandler>();
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"))
    .AddAggregatorResilience(ResilienceProfiles.Catalog)
    .AddHttpMessageHandler<ClientCredentialsHandler>();
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"))
    .AddAggregatorResilience(ResilienceProfiles.Reviews)
    .AddHttpMessageHandler<ClientCredentialsHandler>();

// Лаб. 07: health checks залежностей Aggregator. Кожен сервіс відповідає на стандартний gRPC Health Checking Protocol
// (Check); недоступність сервісу = Unhealthy, недоступність Redis = Degraded (кеш необов'язковий).
builder.Services.AddGrpcDependencyHealthCheck(OrdersClient.ServiceName, "http://_grpc.orders-api");
builder.Services.AddGrpcDependencyHealthCheck(CatalogClient.ServiceName, "http://_grpc.catalog-api");
builder.Services.AddGrpcDependencyHealthCheck(ReviewsClient.ServiceName, "http://_grpc.reviews-api");
builder.Services.AddHealthChecks()
    .AddCheck<RedisCacheHealthCheck>(
        name: "redis-cache",
        failureStatus: HealthStatus.Degraded,
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(3));

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

// callback-и circuit breaker логують через ILoggerFactory, який доступний лише після Build()
ResilienceLogging.Initialize(app.Services.GetRequiredService<ILoggerFactory>());

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

app.UseGatewayOnly();                  // Лаб. 09: прямий доступ повз Gateway блокується (якщо задано Gateway:Secret)
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();                      // /openapi/v1.json
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
