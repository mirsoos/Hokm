using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MongoDB.Bson;
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
                    cm.MapMember(c => c.DealerId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    cm.MapMember(c => c.TrumpSuit).SetSerializer(new EnumSerializer<Suit>(BsonType.String));
                    cm.SetIgnoreExtraElements(true);
                });
            }
            _isConfigured = true;
        }
    }
}

