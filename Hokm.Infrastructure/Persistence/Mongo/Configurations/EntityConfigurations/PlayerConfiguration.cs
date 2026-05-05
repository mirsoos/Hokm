using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations
{
    public class PlayerConfiguration : IEntityConfiguration
    {
        private static bool _isConfigured = false;
        public void Configure()
        {
            if (_isConfigured) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(Player)))
            {
                BsonClassMap.RegisterClassMap<Player>(cm =>
                {
                    cm.AutoMap();
                    cm.MapMember(c => c.PlayerSide).SetSerializer(new EnumSerializer<PlayerSide>(BsonType.String));
                    cm.MapMember(c => c.TeamId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    cm.SetIgnoreExtraElements(true);
                });
            }
            _isConfigured = true;
        }
    }
}
