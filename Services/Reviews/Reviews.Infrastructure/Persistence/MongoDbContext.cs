using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Секція "MongoDb" в appsettings.{Environment}.json.</summary>
public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "";
}

/// <summary>
/// Єдиний на застосунок (singleton) доступ до БД: MongoClient — потокобезпечний і тримає пул з'єднань
/// (maxPoolSize, serverSelectionTimeoutMS задаються в рядку підключення).
/// </summary>
public sealed class MongoDbContext
{
    public const string ReviewsCollection = "reviews";
    public const string BuildsCollection = "builds";

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString is not configured.");
        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
            throw new InvalidOperationException("MongoDb:DatabaseName is not configured.");

        Client = new MongoClient(settings.ConnectionString);
        Database = Client.GetDatabase(settings.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    // write-side: доменні агрегати, серіалізація за BsonClassMap (MongoMappings)
    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>(ReviewsCollection);
    public IMongoCollection<Build> Builds => Database.GetCollection<Build>(BuildsCollection);

    // read-side: сирі документи для aggregation pipeline з проєкцією в read-моделі
    public IMongoCollection<BsonDocument> ReviewDocuments => Database.GetCollection<BsonDocument>(ReviewsCollection);
    public IMongoCollection<BsonDocument> BuildDocuments => Database.GetCollection<BsonDocument>(BuildsCollection);
}
