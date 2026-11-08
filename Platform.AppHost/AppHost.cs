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

var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WaitFor(ordersDb)
    .WithHttpHealthCheck("/health");

// Каталог (Лаб. 03): схему створюють міграції EF Core, сідінг — сам Catalog.Api на старті.
var catalogDb = sql.AddDatabase("CatalogDb", "CustomShop_Catalog");

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

// Відгуки (Лаб. 04): MongoDB у контейнері з постійним volume; індекси й seed створює сам reviews-api на старті.
var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("customshop-reviews-mongo");
var reviewsDb = mongo.AddDatabase("ReviewsDb", "customshop_reviews");

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithHttpHealthCheck("/health");

// Aggregator (Лаб. 05): паралельно опитує сервіси; стартує після них.
// WithReference(orders) дає змінні services__orders-api__http__0 → service discovery за іменем "orders-api".
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews)
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
