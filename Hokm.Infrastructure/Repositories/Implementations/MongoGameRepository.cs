using Hokm.Domain.Entities;
using Hokm.Domain.Interfaces;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using MongoDB.Driver;

namespace Hokm.Infrastructure.Repositories.Implementations
{
    public class MongoGameRepository : IGameRepository
    {
        private readonly MongoDbContext _mongoDb;
        public MongoGameRepository(MongoDbContext mongoDb)
        {
            _mongoDb = mongoDb;
        }
        public async Task<bool> ExistsAsync(Guid gameId)
        {
            return await _mongoDb.Games.CountAsync(x=>x.Id == gameId) > 0;
        }

        public async Task<Game> GetByIdAsync(Guid gameId)
        {
            return await _mongoDb.Games.Find(x => x.Id == gameId).FirstOrDefaultAsync();
        }

        public async Task<Game> SaveAsync(Game game)
        {
            await _mongoDb.Games.InsertOneAsync(game);
            return game;
        }

        public async Task UpdateAsync(Game game)
        {
            await _mongoDb.Games.ReplaceOneAsync(x=>x.Id == game.Id ,game);
        }
    }
}
