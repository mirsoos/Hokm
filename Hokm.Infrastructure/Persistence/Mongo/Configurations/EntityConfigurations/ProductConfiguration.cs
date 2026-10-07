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

            if (!BsonClassMap.IsClassMapRegistered(typeof(ProductItem)))
            {
                BsonClassMap.RegisterClassMap<ProductItem>(cm =>
                {
                    cm.AutoMap();
                    cm.MapMember(c => c.Content).SetElementName("content");
                    cm.MapMember(c => c.LinkedProductId).SetElementName("linkedProductId");
                    cm.MapMember(c => c.Quantity).SetElementName("quantity");
                    cm.MapMember(c => c.SortOrder).SetElementName("sortOrder");
                    cm.MapMember(c => c.IsActive).SetElementName("isActive");
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Product)))
            {
                BsonClassMap.RegisterClassMap<Product>(cm =>
                {
                    cm.AutoMap();

                    cm.MapMember(c => c.ProductType).SetSerializer(new EnumSerializer<ProductType>(BsonType.String));
                    cm.MapMember(c => c.PaymentType).SetSerializer(new EnumSerializer<PaymentType>(BsonType.String));

                    cm.MapMember(c => c.Items).SetElementName("Items");
                    cm.SetIgnoreExtraElements(true);
                });
            }

            _isConfigured = true;
        }
    }
}