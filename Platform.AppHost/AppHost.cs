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

// RabbitMQ (Лаб. 08): брокер повідомлень у Docker-контейнері; облікові дані генерує Aspire і передає сервісам рядком підключення.
// WithDataVolume — черги й persistent-повідомлення переживають перезапуск; WithManagementPlugin — веб-інтерфейс керування (порт 15672).
var rabbit = builder.AddRabbitMQ("messaging")
    .WithDataVolume("customshop-rabbitmq")
    .WithManagementPlugin(port: 15672);

// Endpoint-и сервісів ("http" для REST і "grpc" лише з HTTP/2) Aspire бере із секції Kestrel:Endpoints їхніх appsettings.json.
var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WaitFor(ordersDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(rabbit)                                   // Лаб. 08: споживає події Каталогу (копія довідника Products)
    .WaitFor(rabbit)
    .WithHttpHealthCheck("/health", endpointName: "http");

// Каталог (Лаб. 03): схему створюють міграції EF Core, сідінг — сам Catalog.Api на старті.
var catalogDb = sql.AddDatabase("CatalogDb", "CustomShop_Catalog");

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(rabbit)                                   // Лаб. 08: публікує події про зміну товару
    .WaitFor(rabbit)
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
    .WithReference(rabbit)                                   // Лаб. 08: споживає події Каталогу (productName у відгуках)
    .WaitFor(rabbit)
    .WithHttpHealthCheck("/health", endpointName: "http");

// ───────── Лаб. 09: автентифікація й авторизація ─────────
// Секрет підпису JWT Auth Service і адмін Keycloak — параметри (dev-значення в appsettings.json AppHost, у production — user-secrets/Key Vault).
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
var keycloakAdmin = builder.AddParameter("keycloak-admin");
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);

const string JwtIssuer = "auth-service";
const string JwtAudience = "microservices-platform";
const string IdentityServerUrl = "http://localhost:5300";
const string KeycloakRealm = "DevRealmDrivers";
// true → сервіси відхиляють запити повз Gateway (заголовок X-Gateway-Secret). Вимкнено за замовчуванням: Swagger UI сервісів викликає їх напряму.
const bool EnforceGatewayOnly = false;

// Частина 1: ASP.NET Identity + JWT — ОКРЕМА БД "IdentityDb" на власному SQL Server (пароль SA — той самий параметр sql-password).
var identityDb = builder.AddSqlServer("identity-sql", sqlPassword)
    .WithDataVolume("customshop-identity-sql")
    .AddDatabase("IdentityDb");

var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("Gateway__Secret", EnforceGatewayOnly ? "gateway-shared-secret-dev" : "")
    .WithHttpHealthCheck("/health", endpointName: "http");

// Частина 2: Duende IdentityServer — окремий SQL Server і БД "IdentityServerDb" (ізоляція безпеки від бізнес-сервісів).
// Порт 5000 зайнятий Gateway (Лаб. 05), тому IdentityServer — на 5300.
var identityServerDb = builder.AddSqlServer("identityserver-sql", sqlPassword)
    .WithDataVolume("customshop-identityserver-sql")
    .AddDatabase("IdentityServerDb");

var identityServer = builder.AddProject<Projects.Platform_IdentityServer>("identityserver")
    .WithReference(identityServerDb)
    .WaitFor(identityServerDb)
    .WithHttpEndpoint(port: 5300);

// Частина 3: Keycloak (Aspire-інтеграція). WithDataVolume зберігає realm і користувачів між перезапусками;
// сам realm, clients, ролі та користувачі створює `pulumi up` з infra/keycloak.
var keycloak = builder.AddKeycloak("keycloak", 8080, keycloakAdmin, keycloakAdminPassword)
    .WithDataVolume("customshop-keycloak");

// Payments — demo-сервіс для перевірки доступу (Лаб. 09): endpoints-заглушки, permissions лише в ролі admin.
var payments = builder.AddProject<Projects.Payments_Api>("payments-api")
    .WithHttpHealthCheck("/health", endpointName: "http");

// Спільна конфігурація автентифікації: сервіс приймає токени ВСІХ трьох провайдерів (схема обирається за claim "iss").
IResourceBuilder<ProjectResource> WithAuth(IResourceBuilder<ProjectResource> project) => project
    .WithReference(keycloak)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithEnvironment("Jwt__Issuer", JwtIssuer)
    .WithEnvironment("Jwt__Audience", JwtAudience)
    .WithEnvironment("IdentityServer__Authority", IdentityServerUrl)
    .WithEnvironment("Keycloak__Realm", KeycloakRealm)
    .WithEnvironment("Keycloak__PublicUrl", "http://localhost:8080")
    .WithEnvironment("Gateway__Secret", EnforceGatewayOnly ? "gateway-shared-secret-dev" : "");

WithAuth(orders);
WithAuth(catalog);
WithAuth(reviews);
WithAuth(payments);

// Aggregator (Лаб. 05–06): паралельно опитує сервіси через gRPC; стартує після них і Redis.
// WithReference(orders) дає змінні services__orders-api__grpc__0 → service discovery за адресою http://_grpc.orders-api.
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews)
    .WithReference(redis)
    .WaitFor(redis)
    .WithEnvironment("IdentityServer__Authority", IdentityServerUrl)   // Лаб. 09: Client Credentials Flow (aggregator_service)
    .WaitFor(identityServer)
    .WithHttpHealthCheck("/health");
WithAuth(aggregator);

// API Gateway (YARP) — єдина точка входу з фіксованим портом 5000; маршрутизує до всіх сервісів і Aggregator.
// Порядок запуску: БД → сервіси → Aggregator → Gateway.
var gateway = builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(auth).WithReference(payments)                      // Лаб. 09: /auth/**, /users/**, /api/payments/**
    .WithReference(aggregator)
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();
WithAuth(gateway);                                                    // Лаб. 09: Gateway валідує JWT перед проксіюванням

builder.Build().Run();
