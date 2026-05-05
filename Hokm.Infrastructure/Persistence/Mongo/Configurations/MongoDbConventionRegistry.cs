using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations
{
    public static class MongoDbConventionRegistry
    {
        private static bool _isConfigured = false;

        public static void Configure()
        {
            if (_isConfigured) return;

            var conventionPack = new ConventionPack
            {
                new IgnoreExtraElementsConvention(true),
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreIfNullConvention(true),
                new ImmutableTypeClassMapConvention()
            };

            ConventionRegistry.Register(
                "HokmConventions",
                conventionPack,
                t => t.Namespace != null && t.Namespace.StartsWith("Hokm.Domain"));

            _isConfigured = true;
        }
    }
}
