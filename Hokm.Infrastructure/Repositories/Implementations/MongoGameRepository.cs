using Hokm.Application.Features.Game.Queries.GetGameHistory;
using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using Hokm.Infrastructure.Services.Redis.Constants;
using MongoDB.Driver;

namespace Hokm.Infrastructure.Repositories.Implementations
{
    public class MongoGameRepository : IGameRepository
    {
        private readonly MongoDbContext _mongoDb;
        private readonly Services.Redis.Interfaces.IRedisCacheService _redisCache;
        private readonly IUserRepository _userRepository; // اضافه شد

        public MongoGameRepository(MongoDbContext mongoDb, Services.Redis.Interfaces.IRedisCacheService redisCache, IUserRepository userRepository)
        {
            _mongoDb = mongoDb;
            _redisCache = redisCache;
            _userRepository = userRepository; // اضافه شد
        }

        public async Task<bool> ExistsAsync(Guid gameId, CancellationToken cancellationToken)
        {
            var cursor = await _mongoDb.Games.Find(x => x.Id == gameId).Limit(1).ToCursorAsync(cancellationToken);
            return await cursor.AnyAsync();
        }

        public async Task<Game> GetByIdAsync(Guid gameId, CancellationToken cancellationToken)
        {
            //var key = RedisCacheKeySchema.GameKey(gameId);
            //var cached = await _redisCache.GetAsync<Game>(key,cancellationToken);
            //if (cached != null)
            //    return cached;

            var game = await _mongoDb.Games.Find(x => x.Id == gameId).FirstOrDefaultAsync(cancellationToken);
            //if(game != null)
            //await _redisCache.SetAsync<Game>(key,game,TimeSpan.FromHours(1), cancellationToken);
            return game;
        }

        public async Task<List<GameHistoryItem>?> GetHistoryByUserIdAsync(Guid userId, int take, CancellationToken cancellationToken)
        {
            var filter = Builders<Game>.Filter.ElemMatch(
                g => g.Players,
                p => p.UserId == userId
            ) & Builders<Game>.Filter.Eq(g => g.Status, GameStatus.Finished);

            var games = await _mongoDb.Games
                .Find(filter)
                .SortByDescending(g => g.CreateDate)
                .Limit(take)
                .ToListAsync(cancellationToken);

            if (games == null || games.Count == 0)
                return null;

            return games.Select(game =>
            {
                bool isWin = game.WinnerPlayers.Contains(userId);
                var opponent = game.Players.FirstOrDefault(p => p.UserId != userId);

                return new GameHistoryItem(
                    GameId: game.Id,
                    Date: game.CreateDate.ToString("yyyy/MM/dd"),
                    IsWin: isWin,
                    ScoreChange: 0,
                    OpponentName: opponent?.Name ?? "ناشناس"
                );
            }).ToList();
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
            await _redisCache.RemoveAsync(key, cancellationToken);
        }

        public async Task<bool> IsGameActiveWithHumanPlayersAsync(Guid gameId, CancellationToken cancellationToken)
        {
            var game = await GetByIdAsync(gameId, cancellationToken);

            if (game == null || game.Status == GameStatus.Finished)
                return false;

            foreach (var player in game.Players)
            {
                var user = await _userRepository.GetByIdAsync(player.UserId, cancellationToken);

                if (user != null && !user.IsBot && !player.IsAutoPlay)
                {
                    return true;
                }
            }

            return false;
        }
    }
}