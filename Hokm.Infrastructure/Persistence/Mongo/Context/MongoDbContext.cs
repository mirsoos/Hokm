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

        private static bool _indexesEnsured = false;
        private static readonly object _indexLock = new object();

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
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            if (_indexesEnsured) return;

            lock (_indexLock)
            {
                if (_indexesEnsured) return;

                try
                {
                    var keys = Builders<Transaction>.IndexKeys
                        .Ascending(t => t.PaymentToken);

                    var options = new CreateIndexOptions<Transaction>
                    {
                        Name = "uniq_payment_token",
                        Unique = true,
                        PartialFilterExpression = Builders<Transaction>.Filter.And(
                            Builders<Transaction>.Filter.Exists(t => t.PaymentToken, true),
                            Builders<Transaction>.Filter.Ne(t => t.PaymentToken, null)
                        )
                    };

                    Transactions.Indexes.CreateOne(
                        new CreateIndexModel<Transaction>(keys, options));

                    _indexesEnsured = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to create index on PaymentToken: {ex.Message}");
                }
            }
        }
    }
}