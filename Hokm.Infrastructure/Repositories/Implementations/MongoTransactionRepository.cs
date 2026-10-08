using Hokm.Application.Exceptions;
using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
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
            try
            {
                await _mongoDb.Transactions.InsertOneAsync(transaction, null, cancellationToken);
            }
            catch (MongoWriteException ex)
                when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                throw new DuplicatePaymentException(transaction.PaymentToken);
            }
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
        public async Task<bool> ExistsByTokenAsync(string paymentToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(paymentToken))
                return false;

            var filter = Builders<Transaction>.Filter.And(
                Builders<Transaction>.Filter.Eq(t => t.PaymentToken, paymentToken),
                Builders<Transaction>.Filter.Eq(t => t.Status, TransactionStatus.Completed)
            );

            var cursor = await _mongoDb.Transactions.Find(filter).Limit(1).ToCursorAsync(cancellationToken);
            return await cursor.AnyAsync(cancellationToken);
        }
    }
}
