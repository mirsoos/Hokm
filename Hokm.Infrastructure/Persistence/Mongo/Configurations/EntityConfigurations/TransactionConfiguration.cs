using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations
{
    public class TransactionConfiguration : IEntityConfiguration
    {
        private static bool _isConfigured = false;

        public void Configure()
        {
            if (_isConfigured) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(Transaction)))
            {
                BsonClassMap.RegisterClassMap<Transaction>(cm =>
                {
                    cm.AutoMap();

                    // تنظیم سریالایزر شناسه‌های Guid به صورت استاندارد باینری
                    cm.MapMember(c => c.UserId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    cm.MapMember(c => c.ProductId).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));

                    // سریالایز کردن اِنام‌های تراکنش به صورت رشته
                    cm.MapMember(c => c.Gateway).SetSerializer(new EnumSerializer<GatewayType>(BsonType.String));
                    cm.MapMember(c => c.Status).SetSerializer(new EnumSerializer<TransactionStatus>(BsonType.String));

                    cm.SetIgnoreExtraElements(true);
                });
            }
            _isConfigured = true;
        }
    }
}
