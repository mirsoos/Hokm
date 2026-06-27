using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using MongoDB.Driver;

namespace Hokm.Infrastructure.Repositories.Implementations
{
    public class MongoUserRepository : IUserRepository
    {
        private readonly MongoDbContext _mongoDb;

        public MongoUserRepository(MongoDbContext mongoDb)
        {
            _mongoDb = mongoDb;
        }

        public async Task<Guid> AddAsync(User user, CancellationToken cancellationToken)
        {
            await _mongoDb.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
            return user.Id;
        }

        public async Task<bool> DeleteByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            var result = await _mongoDb.Users.DeleteOneAsync(x => x.Id == userId, cancellationToken);
            return result.DeletedCount > 0;
        }

        public async Task<bool> ExistByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await _mongoDb.Users.Find(u => u.Id == userId).AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsUserNameAsync(string userName, CancellationToken cancellationToken)
        {
            return await _mongoDb.Users.Find(u => u.UserName == userName).AnyAsync(cancellationToken);
        }

        public async Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await _mongoDb.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
        {
            return await _mongoDb.Users.Find(u => u.PhoneNumber == phoneNumber).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task UpdateAvatarAsync(Guid userId, int avatarRef, CancellationToken cancellationToken)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var update = Builders<User>.Update.Set(u => u.AvatarRef, avatarRef);

            await _mongoDb.Users.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        }

        public async Task UpdateProfileAsync(Guid userId, string fullName, int avatarRef, CancellationToken cancellationToken)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);

            var update = Builders<User>.Update
                .Set(u => u.FullName, fullName)
                .Set(u => u.AvatarRef, avatarRef);

            await _mongoDb.Users.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        }
        public async Task<List<User>> GetFirstFourUsersAsync(CancellationToken cancellationToken)
        {
            return await _mongoDb.Users
                .Find(_ => true)
                .Limit(4)
                .ToListAsync(cancellationToken);
        }
    }
}