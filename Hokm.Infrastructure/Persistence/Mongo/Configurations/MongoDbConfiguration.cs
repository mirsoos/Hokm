using Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations;
using System.Reflection;

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
