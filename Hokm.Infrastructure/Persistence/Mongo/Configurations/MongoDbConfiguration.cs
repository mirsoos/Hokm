using Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations
{
    public static class MongoDbConfiguration
    {
        private static bool _isConfigured = false;
        private static readonly object _lock = new object();

        public static void Configure()
        {
            lock (_lock)
            {
                if (_isConfigured) return;

                // ۱. ثبت سراسری سریالایزر شناسه‌ها به صورت String در اولین خط (قبل از بارگذاری هر کلاس‌مپی)
                try
                {
                    BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
                }
                catch (BsonSerializationException)
                {
                    // اگر در فرآیندهای موازی قبلاً ثبت شده باشد، بدون خطا عبور کند
                }

                MongoDbConventionRegistry.Configure();

                BaseEntityConfiguration.Configure();

                ConfigureAllEntityConfigurations();

                _isConfigured = true;
            }
        }

        private static void ConfigureAllEntityConfigurations()
        {
            var configurationTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IEntityConfiguration).IsAssignableFrom(t)
                         && t.IsClass
                         && !t.IsAbstract
                         && t != typeof(BaseEntityConfiguration));

            foreach (var type in configurationTypes)
            {
                var configuration = (IEntityConfiguration)Activator.CreateInstance(type);
                configuration.Configure();
            }
        }

        public static void ConfigureExplicit()
        {
            lock (_lock)
            {
                if (_isConfigured) return;

                try
                {
                    BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
                }
                catch (BsonSerializationException)
                {
                }

                MongoDbConventionRegistry.Configure();
                BaseEntityConfiguration.Configure();

                new GameConfiguration().Configure();
                new PlayerConfiguration().Configure();
                new RoundConfiguration().Configure();
                new TeamConfiguration().Configure();
                new TrickConfiguration().Configure();

                _isConfigured = true;
            }
        }
    }
}