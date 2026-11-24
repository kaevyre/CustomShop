using Yarp.ReverseProxy.Transforms;
using Platform.ApiGateway;
using Platform.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                          // Serilog, OpenTelemetry, health checks, service discovery

// Лаб. 09: Gateway валідує JWT ПЕРЕД проксіюванням (підпис, issuer, lifetime) — для токенів Auth Service, IdentityServer і Keycloak.
// Audience тут не перевіряється: це робить кінцевий сервіс, який валідує токен ще раз незалежно (без звернення до провайдера).
builder.AddPlatformAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()           // http://orders-api -> реальна адреса від Aspire
    // Варіант А (Лаб. 09, п. 2.4/3.5): Gateway валідує токен і додає X-User-Id (YARP transform).
    // Заголовок Authorization теж форвардиться — сервіси валідують токен незалежно (варіант Б).
    .AddTransforms(transforms => transforms.AddRequestTransform(ctx =>
    {
        ctx.ProxyRequest.Headers.Remove("X-User-Id");     // клієнт не може підробити заголовок
        if (builder.Configuration["Gateway:Secret"] is { Length: > 0 } gatewaySecret)   // доказ, що запит пройшов через Gateway
            ctx.ProxyRequest.Headers.TryAddWithoutValidation(GatewayOnlyExtensions.HeaderName, gatewaySecret);
        if (ctx.HttpContext.User.GetUserId() is { } userId)
            ctx.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);
        return ValueTask.CompletedTask;
    }));

builder.Services.AddSingleton<GatewayMetrics>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();          // ДО MapReverseProxy: генерує X-Correlation-Id на вході в систему
app.UseMiddleware<RoutingLoggingMiddleware>();         // маршрут, кластер, адреса, статус, тривалість, помилки проксі

app.UseAuthentication();                               // Лаб. 09: JWT перед проксіюванням; AuthorizationPolicy маршруту — в appsettings
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "CustomShop API Gateway",
    routes = new[] { "/api/orders", "/api/customers", "/api/products", "/api/categories", "/api/brands",
                     "/api/reviews", "/api/builds", "/api/payments", "/auth/*", "/users/*", "/api/aggregator/orders/{id}/full" }
}));

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();
