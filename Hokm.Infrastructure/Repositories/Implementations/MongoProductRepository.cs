using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using MongoDB.Driver;

namespace Hokm.Infrastructure.Repositories.Implementations
{
    public class MongoProductRepository : IProductRepository
    {
        private readonly MongoDbContext _mongoDb;

        public MongoProductRepository(MongoDbContext mongoDb)
        {
            _mongoDb = mongoDb;
        }

        public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _mongoDb.Products.Find(p => p.Id == id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<Product>> GetActiveProductsAsync(ProductType? type = null,PaymentType? paymentType = null,CancellationToken cancellationToken = default)
        {
            var filter = Builders<Product>.Filter.Eq(p => p.IsActive, true);

            if (type.HasValue)
            {
                filter &= Builders<Product>.Filter.Eq(p => p.ProductType, type.Value);
            }

            if (paymentType.HasValue)
            {
                filter &= Builders<Product>.Filter.Eq(p => p.PaymentType, paymentType.Value);
            }

            return await _mongoDb.Products.Find(filter)
                .ToListAsync(cancellationToken);
        }

        public async Task<Product?> GetByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Product>.Filter.ElemMatch(p => p.Items, i => i.Id == itemId);
            return await _mongoDb.Products.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }
    }
}

