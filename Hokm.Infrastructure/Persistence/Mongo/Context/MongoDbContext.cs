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
        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Product> Products => _database.GetCollection<Product>("Products");
        public IMongoCollection<Transaction> Transactions => _database.GetCollection<Transaction>("Transactions");

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
