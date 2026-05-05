using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations
{
    internal class TeamConfiguration : IEntityConfiguration
    { 
        private static bool _isConfigured = false;
        public void Configure()
        {
            if (_isConfigured) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(Team)))
            {
                BsonClassMap.RegisterClassMap<Team>(cm =>
                {
                    cm.AutoMap();
                    cm.MapMember(c => c.TeamSide).SetSerializer(new EnumSerializer<TeamSide>(BsonType.String));
                    cm.SetIgnoreExtraElements(true);
                });
            }
            _isConfigured = true;
        }
    }
}
