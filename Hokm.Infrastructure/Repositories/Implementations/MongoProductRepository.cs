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

        // تزریق IMongoDatabase که قبلاً در سیستم تنظیم کرده‌اید
        public MongoProductRepository(MongoDbContext mongoDb)
        {
            _mongoDb = mongoDb;
        }

        public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _mongoDb.Products.Find(p => p.Id == id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<Product>> GetActiveProductsAsync(ProductType? type = null, CancellationToken cancellationToken = default)
        {
            // فیلتر اولیه: فقط محصولات فعال نشان داده شوند
            var filter = Builders<Product>.Filter.Eq(p => p.IsActive, true);

            // اگر فیلتر نوع محصول فرستاده شده بود، آن را به کوئری اضافه کند
            if (type.HasValue)
            {
                filter &= Builders<Product>.Filter.Eq(p => p.ProductType, type.Value);
            }

            return await _mongoDb.Products.Find(filter)
                .ToListAsync(cancellationToken);
        }
    }
}

