using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations
{
    public class ProductConfiguration : IEntityConfiguration
    {
        private static bool _isConfigured = false;

        public void Configure()
        {
            if (_isConfigured) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(Product)))
            {
                BsonClassMap.RegisterClassMap<Product>(cm =>
                {
                    cm.AutoMap(); // نقشه‌برداری خودکار ویژگی‌های استاندارد

                    // سریالایز کردن اِنام‌ها به صورت رشته (String) در دیتابیس مونگو
                    cm.MapMember(c => c.ProductType).SetSerializer(new EnumSerializer<ProductType>(BsonType.String));
                    cm.MapMember(c => c.PaymentType).SetSerializer(new EnumSerializer<PaymentType>(BsonType.String));

                    cm.SetIgnoreExtraElements(true);
                });
            }
            _isConfigured = true;
        }
    }
}
