using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using Hokm.Infrastructure.Persistence.Mongo.Context;

namespace Hokm.Infrastructure.Repositories.Implementations
{
    public class MongoUserRepository : IUserRepository
    {
        private readonly MongoDbContext _mongoDb;
        public MongoUserRepository(MongoDbContext mongoDb)
        {
            _mongoDb = mongoDb;
        }

        public async Task<Guid> AddAsync(User user)
        {
            await _mongoDb.Users.InsertOneAsync(user);
            return user.Id;
        }
    }
}
