using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Пароль SA — параметр-секрет: значення береться з user-secrets AppHost (Parameters:sql-password)
// або вводиться в Dashboard під час першого запуску; у коді його немає.
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// Постійний volume: дані БД переживають перезапуск AppHost.
var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume("customshop-orders-sql");

// Ресурс бази OrdersDb: API отримує його рядок підключення як ConnectionStrings__OrdersDb через WithReference.
var ordersDb = sql.AddDatabase("OrdersDb", "CustomShop_Orders");

// Схема, процедури й seed розгортаються лише на порожній БД (див. DatabaseInitializer).
var scriptsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "db", "p1"));
builder.Eventing.Subscribe<ResourceReadyEvent>(ordersDb.Resource, async (@event, cancellationToken) =>
{
    var connectionString = await ordersDb.Resource.ConnectionStringExpression.GetValueAsync(cancellationToken)
        ?? throw new InvalidOperationException("Connection string of 'OrdersDb' is not available.");

    await DatabaseInitializer.ApplyAsync(connectionString, scriptsDirectory, cancellationToken);
});

// Redis (Лаб. 06): розподілений кеш L2 із постійним volume; сервіси отримують ConnectionStrings:redis через WithReference.
var redis = builder.AddRedis("redis")
    .WithDataVolume("customshop-redis");

// Endpoint-и сервісів ("http" для REST і "grpc" лише з HTTP/2) Aspire бере із секції Kestrel:Endpoints їхніх appsettings.json.
var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WaitFor(ordersDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");

// Каталог (Лаб. 03): схему створюють міграції EF Core, сідінг — сам Catalog.Api на старті.
var catalogDb = sql.AddDatabase("CatalogDb", "CustomShop_Catalog");

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");

// Відгуки (Лаб. 04): MongoDB у контейнері з постійним volume; індекси й seed створює сам reviews-api на старті.
var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("customshop-reviews-mongo");
var reviewsDb = mongo.AddDatabase("ReviewsDb", "customshop_reviews");

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");

// Aggregator (Лаб. 05–06): паралельно опитує сервіси через gRPC; стартує після них і Redis.
// WithReference(orders) дає змінні services__orders-api__grpc__0 → service discovery за адресою http://_grpc.orders-api.
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews)
    .WithReference(redis)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health");

// API Gateway (YARP) — єдина точка входу з фіксованим портом 5000; маршрутизує до всіх сервісів і Aggregator.
// Порядок запуску: БД → сервіси → Aggregator → Gateway.
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
