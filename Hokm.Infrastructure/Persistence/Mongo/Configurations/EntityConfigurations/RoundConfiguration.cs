using Hokm.Domain.Entities;
using Hokm.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations
{
    public class RoundConfiguration : IEntityConfiguration
    {
        private static bool _isConfigured = false;

        public void Configure()
        {
            if (_isConfigured) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(Round)))
            {
                BsonClassMap.RegisterClassMap<Round>(cm =>
                {
                    cm.AutoMap();

                    cm.MapMember(c => c.DealerId)
                      .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));

                    // استفاده از سریالایزر توکار که Guid را به‌عنوان رشته ذخیره نمی‌کند
                    // بلکه دیکشنری را به‌صورت ArrayOfDocuments مدیریت می‌کند
                    cm.MapMember(c => c.PlayerHands)
                      .SetSerializer(new GuidDictionaryOfCardListSerializer());

                    cm.SetIgnoreExtraElements(true);
                });
            }

            _isConfigured = true;
        }

        /// <summary>
        /// سریالایزر سفارشی برای Dictionary(Guid, List(Card))
        /// که بدون نیاز به کلاس جنریک گمشده، دیکشنری را به صورت آرایه‌ای از
        /// سندهای { "k": ..., "v": ... } ذخیره می‌کند.
        /// </summary>
        private class GuidDictionaryOfCardListSerializer : SerializerBase<Dictionary<Guid, List<Card>>>
        {
            public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Dictionary<Guid, List<Card>> value)
            {
                var writer = context.Writer;

                writer.WriteStartArray();
                foreach (var kvp in value)
                {
                    writer.WriteStartDocument();
                    writer.WriteName("k");
                    BsonSerializer.Serialize(writer, kvp.Key);      // Guid به صورت Binary ذخیره می‌شود
                    writer.WriteName("v");
                    BsonSerializer.Serialize(writer, kvp.Value);    // List<Card> خودکار سریالایز می‌شود
                    writer.WriteEndDocument();
                }
                writer.WriteEndArray();
            }

            public override Dictionary<Guid, List<Card>> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                var reader = context.Reader;
                var dictionary = new Dictionary<Guid, List<Card>>();

                reader.ReadStartArray();
                while (reader.ReadBsonType() != BsonType.EndOfDocument)
                {
                    reader.ReadStartDocument();
                    reader.ReadName(); // "k"
                    var key = BsonSerializer.Deserialize<Guid>(reader);
                    reader.ReadName(); // "v"
                    var value = BsonSerializer.Deserialize<List<Card>>(reader);
                    reader.ReadEndDocument();

                    dictionary[key] = value;
                }
                reader.ReadEndArray();

                return dictionary;
            }
        }
    }
}