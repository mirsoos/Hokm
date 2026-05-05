using Hokm.Domain.Entities;
using Hokm.Infrastructure.Configurations;
using Hokm.Infrastructure.Persistence.Mongo.Configurations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Hokm.Infrastructure.Persistence.Mongo.Context
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;
        public IMongoCollection<Game> Games => _database.GetCollection<Game>("Games");

        static MongoDbContext()
        {
            MongoDbConfiguration.ConfigureExplicit();
        }

        public MongoDbContext(IMongoClient client, IOptions<InfrastructureSettings> options)
        {
            _database = client.GetDatabase(options.Value.MongoDatabaseName);
        }
    }
}
