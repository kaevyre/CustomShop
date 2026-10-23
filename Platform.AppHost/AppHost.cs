using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Пароль SA фіксований у коді: він записується у volume при першому запуску і не повинен залежати від середовища чи user-secrets.
var sqlPassword = builder.AddParameter("sql-password", "Str0ng!Passw0rd", secret: true);

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

builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WaitFor(ordersDb)
    .WithHttpHealthCheck("/health");

// Каталог (Лаб. 03): схему створюють міграції EF Core, сідінг — сам Catalog.Api на старті.
var catalogDb = sql.AddDatabase("CatalogDb", "CustomShop_Catalog");

builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
