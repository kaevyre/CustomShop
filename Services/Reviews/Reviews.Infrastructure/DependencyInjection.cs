using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Persistence.Queries;
using Reviews.Infrastructure.Persistence.Repositories;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// MongoDB: BSON-мапінг (один раз, до першого звернення до БД), MongoDbContext, репозиторії (write-side),
    /// IReviewQueries/IBuildQueries (read-side), індекси й сідінг на старті, health check.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        MongoMappings.Register();

        services.Configure<MongoDbOptions>(config.GetSection(MongoDbOptions.SectionName));
        // під Aspire рядок підключення приходить як ConnectionStrings__ReviewsDb і має пріоритет
        services.PostConfigure<MongoDbOptions>(options =>
        {
            var aspire = config.GetConnectionString("ReviewsDb");
            if (!string.IsNullOrWhiteSpace(aspire))
                options.ConnectionString = aspire;
        });
        services.Configure<SeedingOptions>(options =>
            options.Enabled = config.GetValue("MongoDb:Seed", true));

        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IBuildRepository, BuildRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();
        services.AddScoped<IBuildQueries, BuildQueries>();

        services.AddSingleton<IndexCreationService>();
        services.AddSingleton<IDataSeeder, ReviewSeeder>();
        services.AddSingleton<IDataSeeder, BuildSeeder>();
        services.AddHostedService<MongoInitializationHostedService>();

        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb", tags: ["db", "mongodb"]);
        return services;
    }
}
