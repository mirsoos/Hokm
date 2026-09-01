using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using MongoDB.Driver;

namespace Hokm.Infrastructure.Repositories.Implementations
{
    public class MongoTransactionRepository : ITransactionRepository
    {
        private readonly MongoDbContext _mongoDb;

        public MongoTransactionRepository(MongoDbContext mongo)
        {
            _mongoDb = mongo;
        }

        public async Task CreateAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            await _mongoDb.Transactions.InsertOneAsync(transaction, null, cancellationToken);
        }

        public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _mongoDb.Transactions.Find(t => t.Id == id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            await _mongoDb.Transactions.ReplaceOneAsync(
                t => t.Id == transaction.Id,
                transaction,
                cancellationToken: cancellationToken);
        }
    }
}
