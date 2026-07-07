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

        public async Task<bool> DeductCoinsAsync(List<Guid> userIds, int amount, CancellationToken cancellationToken)
        {
            using (var session = await _mongoDb.Users.Database.Client.StartSessionAsync(cancellationToken: cancellationToken))
            {
                session.StartTransaction();

                try
                {
                    var bulkOps = userIds.Select(userId => new UpdateOneModel<User>(
                        Builders<User>.Filter.And(
                            Builders<User>.Filter.Eq(u => u.Id, userId),
                            Builders<User>.Filter.Gte(u => u.Coin, amount)
                        ),
                        Builders<User>.Update.Inc(u => u.Coin, -amount)
                    )).ToList();

                    var result = await _mongoDb.Users.BulkWriteAsync(
                        session,
                        bulkOps,
                        new BulkWriteOptions { IsOrdered = false },
                        cancellationToken: cancellationToken
                    );

                    if (result.ModifiedCount != userIds.Count)
                    {
                        throw new InvalidOperationException("یک یا چند کاربر موجودی کافی ندارند یا یافت نشدند.");
                    }

                    await session.CommitTransactionAsync(cancellationToken);
                    return true;
                }
                catch (Exception ex)
                {
                    await session.AbortTransactionAsync(cancellationToken);

                    Console.WriteLine($"خطا در تراکنش کسر سکه گروهی: {ex.Message}");
                    return false;
                }
            }
        }

        public async Task<List<User>> GetRandomBotsAsync(int count, List<Guid> excludeUserIds, CancellationToken cancellationToken)
        {
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.IsBot, true),
                Builders<User>.Filter.Nin(u => u.Id, excludeUserIds)
            );

            return await _mongoDb.Users.Aggregate()
                .Match(filter)
                .Sample(count)
                .ToListAsync(cancellationToken);
        }
    }
}