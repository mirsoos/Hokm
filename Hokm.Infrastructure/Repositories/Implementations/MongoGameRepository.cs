using Hokm.Domain.Entities;
using Hokm.Application.Interfaces;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using MongoDB.Driver;
using Hokm.Infrastructure.Services.Redis.Interfaces;
using Hokm.Infrastructure.Services.Redis.Constants;

namespace Hokm.Infrastructure.Repositories.Implementations
{
    public class MongoGameRepository : IGameRepository
    {
        private readonly MongoDbContext _mongoDb;
        private readonly IRedisCacheService _redisCache;
        public MongoGameRepository(MongoDbContext mongoDb , IRedisCacheService redisCache)
        {
            _mongoDb = mongoDb;
            _redisCache = redisCache;
        }
        public async Task<bool> ExistsAsync(Guid gameId , CancellationToken cancellationToken)
        {
            var cursor = await _mongoDb.Games.Find(x => x.Id == gameId).Limit(1).ToCursorAsync(cancellationToken);
            return await cursor.AnyAsync();
        }

        public async Task<Game> GetByIdAsync(Guid gameId, CancellationToken cancellationToken)
        {
            var key = RedisCacheKeySchema.GameKey(gameId);
            var cached = await _redisCache.GetAsync<Game>(key,cancellationToken);
            if (cached != null)
                return cached;
            
            var game = await _mongoDb.Games.Find(x => x.Id == gameId).FirstOrDefaultAsync(cancellationToken);
            if(game != null)
            await _redisCache.SetAsync<Game>(key,game,TimeSpan.FromHours(1), cancellationToken);
            return game;
        }

        public async Task<Game> SaveAsync(Game game, CancellationToken cancellationToken)
        {
            await _mongoDb.Games.InsertOneAsync(game, cancellationToken: cancellationToken);
            var key = RedisCacheKeySchema.GameKey(game.Id);
            await _redisCache.SetAsync(key, game, TimeSpan.FromHours(1), cancellationToken);
            return game;
        }

        public async Task UpdateAsync(Game game, CancellationToken cancellationToken)
        {
            var key = RedisCacheKeySchema.GameKey(game.Id);
            await _mongoDb.Games.ReplaceOneAsync(x => x.Id == game.Id, game, cancellationToken: cancellationToken);
            await _redisCache.RemoveAsync(key,cancellationToken);
        }
    }
}
