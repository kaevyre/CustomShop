using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Reviews.Infrastructure.Persistence.Serialization;

/// <summary>
/// Вкладений документ customization з гнучкою схемою: у клавіатури switches/keycaps, у миші dpi (int),
/// lubed (bool) тощо. Домен бачить словник .NET-значень, у БД — звичайний підDocument без _t/_v обгорток.
/// </summary>
public sealed class CustomizationSerializer<TDictionary> : SerializerBase<TDictionary>
    where TDictionary : class, IReadOnlyDictionary<string, object>
{
    private readonly Func<Dictionary<string, object>, TDictionary> _factory;

    public CustomizationSerializer(Func<Dictionary<string, object>, TDictionary> factory) => _factory = factory;

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TDictionary value)
    {
        var document = new BsonDocument();
        foreach (var (key, item) in value)
            document[key] = BsonTypeMapper.MapToBsonValue(item);
        BsonDocumentSerializer.Instance.Serialize(context, document);
    }

    public override TDictionary Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.GetCurrentBsonType() == BsonType.Null)
        {
            context.Reader.ReadNull();
            return _factory(new Dictionary<string, object>());
        }

        var document = BsonDocumentSerializer.Instance.Deserialize(context);
        return _factory(ToDictionary(document));
    }

    public static Dictionary<string, object> ToDictionary(BsonDocument? document)
        => document?.Elements.ToDictionary(e => e.Name, e => BsonTypeMapper.MapToDotNetValue(e.Value))
           ?? new Dictionary<string, object>();
}
