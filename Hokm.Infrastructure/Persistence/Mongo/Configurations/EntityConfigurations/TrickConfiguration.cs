using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations
{
    public class TrickConfiguration : IEntityConfiguration
    {
        private static bool _isConfigured = false;
        public void Configure()
        {
            if (_isConfigured) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(Trick)))
            {
                BsonClassMap.RegisterClassMap<Trick>(cm =>
                {
                    cm.AutoMap();

                    cm.MapMember(c => c.LeadPlayerId)
                      .SetSerializer(new NullableSerializer<Guid>(new GuidSerializer(GuidRepresentation.Standard)));
                    cm.MapMember(c => c.WinnerPlayerId)
                      .SetSerializer(new NullableSerializer<Guid>(new GuidSerializer(GuidRepresentation.Standard)));
                    cm.MapMember(c => c.TrumpSuit)
                      .SetSerializer(new NullableSerializer<Suit>(new EnumSerializer<Suit>(BsonType.String)));
                    cm.MapMember(c => c.LedSuit)
                      .SetSerializer(new NullableSerializer<Suit>(new EnumSerializer<Suit>(BsonType.String)));

                    cm.MapMember(c => c.PlayedCards)
                      .SetSerializer(new GuidCardDictionarySerializer());

                    cm.SetIgnoreExtraElements(true);
                });
            }
            _isConfigured = true;
        }

        private class GuidCardDictionarySerializer : SerializerBase<Dictionary<Guid, Card>>
        {
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Dictionary<Guid, Card> value)
            {
                var writer = context.Writer;
                writer.WriteStartArray();
                foreach (var kvp in value)
                {
                    writer.WriteStartDocument();
                    writer.WriteName("k");
                    BsonSerializer.Serialize(writer, kvp.Key);
                    writer.WriteName("v");
                    BsonSerializer.Serialize(writer, kvp.Value);
                    writer.WriteEndDocument();
                }
                writer.WriteEndArray();
            }

            public override Dictionary<Guid, Card> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                var reader = context.Reader;
                var dict = new Dictionary<Guid, Card>();
                reader.ReadStartArray();
                while (reader.ReadBsonType() != BsonType.EndOfDocument)
                {
                    reader.ReadStartDocument();
                    reader.ReadName();
                    var key = BsonSerializer.Deserialize<Guid>(reader);
                    reader.ReadName();
                    var val = BsonSerializer.Deserialize<Card>(reader);
                    reader.ReadEndDocument();
                    dict[key] = val;
                }
                reader.ReadEndArray();
                return dict;
            }
        }
    }
}