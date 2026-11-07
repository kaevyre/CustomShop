using Platform.ApiGateway;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                          // Serilog, OpenTelemetry, health checks, service discovery

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();          // http://orders-api -> реальна адреса від Aspire

builder.Services.AddSingleton<GatewayMetrics>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();          // ДО MapReverseProxy: генерує X-Correlation-Id на вході в систему
app.UseMiddleware<RoutingLoggingMiddleware>();         // маршрут, кластер, адреса, статус, тривалість, помилки проксі

app.MapGet("/", () => Results.Ok(new
{
    service = "CustomShop API Gateway",
    routes = new[] { "/api/orders", "/api/customers", "/api/products", "/api/categories", "/api/brands",
                     "/api/reviews", "/api/builds", "/api/aggregator/orders/{id}/full" }
}));

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();
