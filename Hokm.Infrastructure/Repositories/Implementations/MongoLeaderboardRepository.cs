using Hokm.Application.Interfaces;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Hokm.Infrastructure.Repositories.Implementations
{
    public class MongoLeaderboardRepository : ILeaderboardRepository
    {
        private readonly MongoDbContext _mongoDb;

        public MongoLeaderboardRepository(MongoDbContext mongoDb)
        {
            _mongoDb = mongoDb;
        }

        public Task<List<Guid>> GetTopWeeklyWinnerIdsAsync(int days, int limit, CancellationToken ct)
            => GetTopWinnerIdsInRangeAsync(DateTime.UtcNow.AddDays(-days), limit, ct);

        public Task<List<Guid>> GetTopMonthlyWinnerIdsAsync(int days, int limit, CancellationToken ct)
            => GetTopWinnerIdsInRangeAsync(DateTime.UtcNow.AddDays(-days), limit, ct);

        private async Task<List<Guid>> GetTopWinnerIdsInRangeAsync(
            DateTime since, int limit, CancellationToken ct)
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    { "status", "Finished" },
                    { "tableKind", new BsonDocument("$ne", "Bot") },
                    { "createDate", new BsonDocument("$gte", since) },
                    { "WinnerPlayers", new BsonDocument("$ne", new BsonArray()) }
                }),
                new BsonDocument("$unwind", "$WinnerPlayers"),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$WinnerPlayers" },
                    { "wins", new BsonDocument("$sum", 1) }
                }),
                new BsonDocument("$sort", new BsonDocument("wins", -1)),
                new BsonDocument("$limit", limit)
            };

            var docs = await _mongoDb.Games
                .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
                .ToListAsync(ct);

            return docs
                .Where(d => d.Contains("_id") && !d["_id"].IsBsonNull)
                .Select(d => d["_id"].AsGuid)
                .ToList();
        }

        public async Task<List<Guid>> GetTopWinrateIdsAsync(int minGames, int limit, CancellationToken ct)
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    { "isBot", new BsonDocument("$ne", true) },
                    { "$expr", new BsonDocument("$gte", new BsonArray
                        {
                            new BsonDocument("$add", new BsonArray { "$wins", "$loses" }),
                            minGames
                        })
                    }
                }),
                new BsonDocument("$addFields", new BsonDocument
                {
                    { "winrate", new BsonDocument("$divide", new BsonArray
                        {
                            "$wins",
                            new BsonDocument("$add", new BsonArray { "$wins", "$loses" })
                        })
                    }
                }),
                new BsonDocument("$sort", new BsonDocument
                {
                    { "winrate", -1 },
                    { "wins", -1 }
                }),
                new BsonDocument("$limit", limit)
            };

            var docs = await _mongoDb.Users
                .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
                .ToListAsync(ct);

            return docs
                .Where(d => d.Contains("_id") && !d["_id"].IsBsonNull)
                .Select(d => d["_id"].AsGuid)
                .ToList();
        }

        public Task<int> GetWeeklyRankAsync(Guid userId, int days, CancellationToken ct)
            => GetWinnerRankAsync(userId, DateTime.UtcNow.AddDays(-days), ct);

        public Task<int> GetMonthlyRankAsync(Guid userId, int days, CancellationToken ct)
            => GetWinnerRankAsync(userId, DateTime.UtcNow.AddDays(-days), ct);

        private async Task<int> GetWinnerRankAsync(Guid userId, DateTime since, CancellationToken ct)
        {
            var userPipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    { "status", "Finished" },
                    { "tableKind", new BsonDocument("$ne", "Bot") },
                    { "createDate", new BsonDocument("$gte", since) },
                    { "WinnerPlayers", BsonValue.Create(userId) }
                }),
                new BsonDocument("$count", "wins")
            };

            var userDocs = await _mongoDb.Games
                .Aggregate<BsonDocument>(userPipeline, cancellationToken: ct)
                .ToListAsync(ct);

            int myWins = userDocs.Count > 0 ? userDocs[0]["wins"].AsInt32 : 0;

            if (myWins == 0) return 0;

            var countPipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    { "status", "Finished" },
                    { "tableKind", new BsonDocument("$ne", "Bot") },
                    { "createDate", new BsonDocument("$gte", since) },
                    { "WinnerPlayers", new BsonDocument("$ne", new BsonArray()) }
                }),
                new BsonDocument("$unwind", "$WinnerPlayers"),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$WinnerPlayers" },
                    { "wins", new BsonDocument("$sum", 1) }
                }),
                new BsonDocument("$match", new BsonDocument("wins",
                    new BsonDocument("$gt", myWins))),
                new BsonDocument("$count", "greater")
            };

            var countDocs = await _mongoDb.Games
                .Aggregate<BsonDocument>(countPipeline, cancellationToken: ct)
                .ToListAsync(ct);

            int greater = countDocs.Count > 0 ? countDocs[0]["greater"].AsInt32 : 0;
            return greater + 1;
        }

        public async Task<int> GetWinrateRankAsync(Guid userId, int minGames, CancellationToken ct)
        {
            var user = await _mongoDb.Users
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync(ct);

            if (user == null) return 0;
            int totalGames = user.Wins + user.Loses;
            if (totalGames < minGames) return 0;

            double myWinrate = (double)user.Wins / totalGames;

            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument
                {
                    { "isBot", new BsonDocument("$ne", true) },
                    { "_id", new BsonDocument("$ne", BsonValue.Create(userId)) },
                    { "$expr", new BsonDocument("$gte", new BsonArray
                        {
                            new BsonDocument("$add", new BsonArray { "$wins", "$loses" }),
                            minGames
                        })
                    }
                }),
                new BsonDocument("$addFields", new BsonDocument
                {
                    { "winrate", new BsonDocument("$divide", new BsonArray
                        {
                            "$wins",
                            new BsonDocument("$add", new BsonArray { "$wins", "$loses" })
                        })
                    }
                }),
                new BsonDocument("$match", new BsonDocument("winrate",
                    new BsonDocument("$gt", myWinrate))),
                new BsonDocument("$count", "greater")
            };

            var docs = await _mongoDb.Users
                .Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
                .ToListAsync(ct);

            int greater = docs.Count > 0 ? docs[0]["greater"].AsInt32 : 0;
            return greater + 1;
        }
    }
}