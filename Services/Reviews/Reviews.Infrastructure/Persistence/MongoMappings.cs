using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence.Serialization;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// BSON-мапінг доменних класів без жодного атрибута в Domain. Реєструється один раз на старті,
/// до першого звернення до БД (BsonClassMap глобальний на процес, повторна реєстрація кидає виняток).
/// </summary>
public static class MongoMappings
{
    private static readonly object Sync = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Sync)
        {
            if (_registered) return;

            // camelCase як у документах Лаб. 01; null-поля не пишемо (JSON Schema не пускає null у string-поля);
            // enum — рядком ("Keyboard"); зайві поля в документі не ламають десеріалізацію.
            ConventionRegistry.Register("reviews-conventions",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true),
                    new IgnoreIfNullConvention(true),
                    new EnumRepresentationConvention(BsonType.String)
                },
                type => type.Namespace?.StartsWith("Reviews.", StringComparison.Ordinal) == true);

            BsonSerializer.TryRegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));

            BsonClassMap.TryRegisterClassMap<BaseEntity>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(e => e.Id).SetSerializer(new ObjectIdSerializer());
                cm.MapMember(e => e.Version).SetDefaultValue(0);   // у документах Лаб. 01 поля version ще немає
            });

            // Агрегати мапляться явно, без AutoMap: усі setter-и приватні, тому AutoMap вважає клас незмінним
            // і бере публічний конструктор (з валідацією) як creator. Тут же десеріалізація йде через
            // приватний конструктор без параметрів + приватні setter-и/поля.
            BsonClassMap.TryRegisterClassMap<Review>(cm =>
            {
                cm.SetIgnoreExtraElements(true);
                cm.MapMember(r => r.ProductId).SetElementName("productId");
                cm.MapMember(r => r.ProductName).SetElementName("productName");
                cm.MapMember(r => r.ProductType).SetElementName("productType").SetIgnoreIfNull(true)
                    .SetSerializer(new NullableSerializer<ProductType>(new EnumSerializer<ProductType>(BsonType.String)));
                cm.MapMember(r => r.Author).SetElementName("author");
                cm.MapMember(r => r.Rating).SetElementName("rating").SetSerializer(new RatingSerializer());
                cm.MapMember(r => r.Title).SetElementName("title").SetIgnoreIfNull(true);
                cm.MapMember(r => r.Text).SetElementName("text");
                cm.MapMember(r => r.VerifiedPurchase).SetElementName("verifiedPurchase");
                cm.MapMember(r => r.OrderId).SetElementName("orderId").SetIgnoreIfNull(true);
                cm.MapMember(r => r.HelpfulVotes).SetElementName("helpfulVotes");
                cm.MapMember(r => r.DiscussionId).SetElementName("discussionId").SetIgnoreIfNull(true);

                // приватні поля колекцій; порожні необов'язкові масиви не пишемо (як мінімальний відгук у Лаб. 01)
                cm.MapField("_comments").SetElementName("comments");
                cm.MapField("_pros").SetElementName("pros").SetShouldSerializeMethod(r => ((Review)r).Pros.Count > 0);
                cm.MapField("_cons").SetElementName("cons").SetShouldSerializeMethod(r => ((Review)r).Cons.Count > 0);
                cm.MapField("_photos").SetElementName("photos").SetShouldSerializeMethod(r => ((Review)r).Photos.Count > 0);
                cm.MapField("_customization").SetElementName("customization")
                    .SetSerializer(new CustomizationSerializer<Dictionary<string, object>>(d => d))
                    .SetShouldSerializeMethod(r => ((Review)r).Customization.Count > 0);
            });

            BsonClassMap.TryRegisterClassMap<Comment>(cm =>
            {
                cm.MapMember(c => c.Author).SetElementName("author");
                cm.MapMember(c => c.Text).SetElementName("text");
                cm.MapMember(c => c.CreatedAt).SetElementName("createdAt");
            });

            BsonClassMap.TryRegisterClassMap<Build>(cm =>
            {
                cm.SetIgnoreExtraElements(true);
                cm.MapMember(b => b.Title).SetElementName("title");
                cm.MapMember(b => b.Description).SetElementName("description").SetIgnoreIfNull(true);
                cm.MapMember(b => b.Author).SetElementName("author");
                cm.MapMember(b => b.Likes).SetElementName("likes");
                cm.MapField("_items").SetElementName("items");
                cm.MapField("_tags").SetElementName("tags");
                cm.MapField("_photos").SetElementName("photos").SetShouldSerializeMethod(b => ((Build)b).Photos.Count > 0);
            });

            // Value Objects: незмінні, створюються лише через конструктор (з його валідацією)
            BsonClassMap.TryRegisterClassMap<Author>(cm =>
            {
                cm.MapProperty(a => a.ProfileId).SetElementName("profileId");
                cm.MapProperty(a => a.DisplayName).SetElementName("displayName");
                cm.MapCreator(a => new Author(a.ProfileId, a.DisplayName));
            });

            BsonClassMap.TryRegisterClassMap<BuildItem>(cm =>
            {
                cm.MapProperty(i => i.ProductId).SetElementName("productId");
                cm.MapProperty(i => i.ProductName).SetElementName("productName");
                cm.MapProperty(i => i.ProductType).SetElementName("productType")
                    .SetSerializer(new EnumSerializer<ProductType>(BsonType.String));
                cm.MapProperty(i => i.Customization).SetElementName("customization")
                    .SetSerializer(new CustomizationSerializer<IReadOnlyDictionary<string, object>>(d => d))
                    .SetShouldSerializeMethod(i => ((BuildItem)i).Customization.Count > 0)
                    .SetDefaultValue(() => new Dictionary<string, object>());
                cm.MapProperty(i => i.Note).SetElementName("note").SetIgnoreIfNull(true).SetDefaultValue((object?)null);
                // необов'язкові елементи (customization, note) мають значення за замовчуванням — інакше creator не обереться
                cm.MapCreator(i => new BuildItem(i.ProductId, i.ProductName, i.ProductType, i.Customization, i.Note));
            });

            _registered = true;
        }
    }
}
