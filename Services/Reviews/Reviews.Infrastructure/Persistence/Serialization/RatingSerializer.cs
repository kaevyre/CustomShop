using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence.Serialization;

/// <summary>Value Object Rating ↔ int32 (поле rating у документі, як у JSON Schema Лаб. 01).</summary>
public sealed class RatingSerializer : SerializerBase<Rating>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Rating value)
        => context.Writer.WriteInt32(value.Value);

    public override Rating Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        var value = reader.GetCurrentBsonType() switch
        {
            BsonType.Int32 => reader.ReadInt32(),
            BsonType.Int64 => (int)reader.ReadInt64(),
            BsonType.Double => (int)reader.ReadDouble(),
            var type => throw new FormatException($"Cannot deserialize Rating from BsonType {type}")
        };
        return new Rating(value);                               // валідація 1–5 у конструкторі VO
    }
}
