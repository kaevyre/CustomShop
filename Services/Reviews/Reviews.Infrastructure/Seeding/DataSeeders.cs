using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>Сідер однієї колекції; має бути ідемпотентним — повторний запуск не створює дублів.</summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}

/// <summary>Профілі з db/p3/seed.js — ті самі ObjectId, щоб посилання author.profileId збігалися з Лаб. 01.</summary>
internal static class SeedProfiles
{
    public static readonly Author Olena = new(ObjectId.Parse("66f0c2a1e4b0a1b2c3d40001"), "Олена К.");
    public static readonly Author Andrii = new(ObjectId.Parse("66f0c2a1e4b0a1b2c3d40002"), "Andrii_M");
    public static readonly Author Maria = new(ObjectId.Parse("66f0c2a1e4b0a1b2c3d40003"), "Марія");
    public static readonly Author Taras = new(ObjectId.Parse("66f0c2a1e4b0a1b2c3d40004"), "Тарас Б.");
}

public sealed class ReviewSeeder(MongoDbContext context, ILogger<ReviewSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // колекцію вже наповнено (цим сідером або скриптом db/p3/seed.js) — нічого не робимо
        if (await context.Reviews.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
        {
            logger.LogInformation("Seeding skipped: collection 'reviews' is not empty");
            return;
        }

        const string aurora = "Клавіатура Aurora 75 (алюміній)";
        var auroraReview = new Review(1, aurora, ProductType.Keyboard, SeedProfiles.Olena, new Rating(5),
            "Найкраща клавіатура, яку я мала",
            "Корпус важкий і не дзвенить, Holy Panda відчуваються дуже тактильно. Кейкапи Retro Beige виглядають ще краще, ніж на фото.",
            pros: ["Якість корпусу", "Звук", "Швидке виготовлення"], cons: ["Важка для поїздок"],
            customization: new Dictionary<string, object>
                { ["switches"] = "Holy Panda", ["keycaps"] = "Retro Beige", ["caseColor"] = "Silver" },
            orderId: 1);
        auroraReview.AddComment("Andrii_M", "Які стабілізатори стоять?");
        auroraReview.AddComment("Олена К.", "Заводські, змащені — не тарахтять");

        var reviews = new[]
        {
            auroraReview,
            new Review(5, "Килимок Canvas XL 900×400", ProductType.Mousepad, SeedProfiles.Olena, new Rating(4), null,
                "Принт яскравий, край прошитий акуратно. Перші дні був запах гуми.",
                customization: new Dictionary<string, object>
                    { ["print"] = "Власний принт", ["printUrl"] = "prints/olena-retro.png", ["size"] = "900×400" },
                orderId: 1),
            new Review(8, "Клавіатура Pocket 60 Hot-Swap", ProductType.Keyboard, SeedProfiles.Andrii, new Rating(4),
                "Ідеальна для мінімалістичного столу",
                "Hot-swap працює без проблем, поставив Kailh Box White. Шкода, що немає бездротового режиму.",
                pros: ["Hot-swap", "Компактність"], cons: ["Лише дротова"],
                customization: new Dictionary<string, object>
                    { ["switches"] = "Kailh Box White", ["plate"] = "FR4", ["lubed"] = true }),
            new Review(4, "Колонки DeskBeat 2.0", null, SeedProfiles.Maria, new Rating(3), null,
                "Звук нормальний, але басу замало."),
            new Review(1, aurora, ProductType.Keyboard, SeedProfiles.Taras, new Rating(4), null,
                "Дуже добра, але ціна з опціями виходить відчутна.",
                cons: ["Ціна з Holy Panda + Retro Beige"])
        };

        await context.Reviews.InsertManyAsync(reviews, cancellationToken: ct);   // bulk-вставка одним запитом
        logger.LogInformation("Seeded {Count} reviews", reviews.Length);
    }
}

public sealed class BuildSeeder(MongoDbContext context, ILogger<BuildSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.Builds.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
        {
            logger.LogInformation("Seeding skipped: collection 'builds' is not empty");
            return;
        }

        var builds = new[]
        {
            new Build("Retro beige desk", "Сетап у кольорах старих ПК", SeedProfiles.Olena,
            [
                new BuildItem(1, "Клавіатура Aurora 75 (алюміній)", ProductType.Keyboard,
                    new Dictionary<string, object> { ["switches"] = "Holy Panda", ["keycaps"] = "Retro Beige", ["caseColor"] = "Silver" }),
                new BuildItem(5, "Килимок Canvas XL 900×400", ProductType.Mousepad,
                    new Dictionary<string, object> { ["size"] = "900×400", ["printUrl"] = "prints/olena-retro.png" }),
                new BuildItem(7, "Кабель Coil USB-C", ProductType.Accessory,
                    new Dictionary<string, object> { ["length"] = "1.5 м", ["connector"] = "GX16" })
            ], ["retro", "beige", "75%"]),
            new Build("Minimal white 60%", null, SeedProfiles.Andrii,
            [
                new BuildItem(8, "Клавіатура Pocket 60 Hot-Swap", ProductType.Keyboard,
                    new Dictionary<string, object> { ["switches"] = "Kailh Box White", ["plate"] = "FR4" }),
                new BuildItem(2, "Миша Glide Pro", ProductType.Mouse,
                    new Dictionary<string, object> { ["caseColor"] = "Lilac", ["coating"] = "Matte", ["dpi"] = 1600 },
                    "чекаю з виготовлення")
            ], ["minimal", "60%", "white"]),
            new Build("Audio corner", null, SeedProfiles.Maria,
            [
                new BuildItem(3, "Навушники Studio Wave", ProductType.Headphones,
                    new Dictionary<string, object> { ["earpads"] = "Leather", ["engraving"] = "M.S." }),
                new BuildItem(4, "Колонки DeskBeat 2.0", ProductType.Speakers, note: "на підставках під кутом 15°")
            ], ["audio"])
        };

        await context.Builds.InsertManyAsync(builds, cancellationToken: ct);
        logger.LogInformation("Seeded {Count} builds", builds.Length);
    }
}
