using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MongoDB.Bson;
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
                    cm.MapMember(c => c.LeadPlayerId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    cm.MapMember(c => c.WinnerPlayerId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    cm.MapMember(c => c.TrumpSuit).SetSerializer(new EnumSerializer<Suit>(BsonType.String));
                    cm.MapMember(c => c.LedSuit).SetSerializer(new NullableSerializer<Suit>(new EnumSerializer<Suit>(BsonType.String)));
                    cm.SetIgnoreExtraElements(true);
                });
            }
            _isConfigured = true;
        }
    }
}
