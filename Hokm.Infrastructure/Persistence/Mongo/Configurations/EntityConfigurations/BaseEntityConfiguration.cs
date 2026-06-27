using Hokm.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations
{
    public static class BaseEntityConfiguration
    {
        public static void Configure()
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(BaseEntity)))
            {
                BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(be => be.Id).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    cm.MapMember(be => be.RowVersion).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    cm.MapMember(be => be.CreateDate).SetSerializer(new DateTimeSerializer(DateTimeKind.Utc));
                    cm.SetIgnoreExtraElements(true);
                    cm.SetIsRootClass(true);
                });
            }
        }
    }
}
