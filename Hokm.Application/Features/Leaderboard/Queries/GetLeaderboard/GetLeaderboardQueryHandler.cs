using ErrorOr;
using Hokm.Application.Interfaces;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.Leaderboard.Queries.GetLeaderboard
{
    public class GetLeaderboardQueryHandler
            : IRequestHandler<GetLeaderboardQuery, ErrorOr<LeaderboardResultDto>>
    {
        private readonly ILeaderboardRepository _leaderboardRepo;
        private readonly IUserRepository _userRepo;
        private readonly IProductRepository _productRepo;
        private readonly IRedisCacheService _cache;

        private const int TopLimit = 50;
        private const int MinGamesForWinrate = 10;
        private const int WeeklyDays = 7;
        private const int MonthlyDays = 30;

        private static readonly TimeSpan ListCacheTtl = TimeSpan.FromHours(3);
        private static readonly TimeSpan RankCacheTtl = TimeSpan.FromMinutes(30);

        public GetLeaderboardQueryHandler(
            ILeaderboardRepository leaderboardRepo,
            IUserRepository userRepo,
            IProductRepository productRepo,
            IRedisCacheService cache)
        {
            _leaderboardRepo = leaderboardRepo;
            _userRepo = userRepo;
            _productRepo = productRepo;
            _cache = cache;
        }

        public async Task<ErrorOr<LeaderboardResultDto>> Handle(
            GetLeaderboardQuery request,
            CancellationToken cancellationToken)
        {
            var listKey = request.Type switch
            {
                LeaderboardType.Weekly => "leaderboard:weekly:v1",
                LeaderboardType.Monthly => "leaderboard:monthly:v1",
                LeaderboardType.Winrate => "leaderboard:winrate:v1",
                _ => "leaderboard:weekly:v1"
            };

            var entries = await _cache.GetAsync<List<LeaderboardEntryDto>>(listKey, cancellationToken);

            if (entries == null || entries.Count == 0)
            {
                var ids = await FetchTopIdsAsync(request.Type, cancellationToken);
                if (ids.Count == 0)
                    return new LeaderboardResultDto(new(), 0, false);

                entries = await BuildEntriesAsync(ids, cancellationToken);
                await _cache.SetAsync(listKey, entries, ListCacheTtl, cancellationToken);
            }

            // رتبه‌ی خود کاربر
            int myRank = 0;
            bool inTop = false;

            var myEntry = entries.FirstOrDefault(e => e.UserId == request.UserId);
            if (myEntry != null)
            {
                myRank = myEntry.Rank;
                inTop = true;
            }
            else
            {
                var rankKey = $"rank:{request.Type.ToString().ToLower()}:{request.UserId}";
                var cachedRank = await _cache.GetAsync<int?>(rankKey, cancellationToken);
                if (cachedRank.HasValue)
                {
                    myRank = cachedRank.Value;
                }
                else
                {
                    myRank = await FetchRankAsync(request.Type, request.UserId, cancellationToken);
                    await _cache.SetAsync(rankKey, myRank, RankCacheTtl, cancellationToken);
                }
            }

            return new LeaderboardResultDto(entries, myRank, inTop);
        }

        private async Task<List<Guid>> FetchTopIdsAsync(LeaderboardType type, CancellationToken ct)
        {
            switch (type)
            {
                case LeaderboardType.Weekly:
                    return await _leaderboardRepo.GetTopWeeklyWinnerIdsAsync(WeeklyDays, TopLimit, ct);
                case LeaderboardType.Monthly:
                    return await _leaderboardRepo.GetTopMonthlyWinnerIdsAsync(MonthlyDays, TopLimit, ct);
                case LeaderboardType.Winrate:
                    return await _leaderboardRepo.GetTopWinrateIdsAsync(MinGamesForWinrate, TopLimit, ct);
                default:
                    return new List<Guid>();
            }
        }

        private async Task<int> FetchRankAsync(LeaderboardType type, Guid userId, CancellationToken ct)
        {
            switch (type)
            {
                case LeaderboardType.Weekly:
                    return await _leaderboardRepo.GetWeeklyRankAsync(userId, WeeklyDays, ct);
                case LeaderboardType.Monthly:
                    return await _leaderboardRepo.GetMonthlyRankAsync(userId, MonthlyDays, ct);
                case LeaderboardType.Winrate:
                    return await _leaderboardRepo.GetWinrateRankAsync(userId, MinGamesForWinrate, ct);
                default:
                    return 0;
            }
        }

        private async Task<List<LeaderboardEntryDto>> BuildEntriesAsync(
            List<Guid> ids,
            CancellationToken ct)
        {
            // ۱. همه‌ی کاربران را یک‌بار می‌گیریم
            var users = await _userRepo.GetByIdsAsync(ids, ct);
            var userMap = users.ToDictionary(u => u.Id);

            // ۲. تمام BorderIdهای یکتا را جمع می‌کنیم و یک‌بار استعلام می‌گیریم
            var borderIds = users
                .Where(u => u.ActiveAvatarBorderId.HasValue)
                .Select(u => u.ActiveAvatarBorderId!.Value)
                .Distinct()
                .ToList();

            var borderAssetMap = new Dictionary<Guid, string>();
            if (borderIds.Count > 0)
            {
                var products = await _productRepo.GetByIdsAsync(borderIds, ct);
                foreach (var p in products)
                {
                    if (!string.IsNullOrEmpty(p.AssetKey))
                        borderAssetMap[p.Id] = p.AssetKey;
                }
            }

            // ۳. ساخت لیست نهایی
            var result = new List<LeaderboardEntryDto>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                if (!userMap.TryGetValue(ids[i], out var user)) continue;

                string? borderKey = null;
                if (user.ActiveAvatarBorderId.HasValue &&
                    borderAssetMap.TryGetValue(user.ActiveAvatarBorderId.Value, out var k))
                {
                    borderKey = k;
                }

                result.Add(new LeaderboardEntryDto(
                    Rank: i + 1,
                    UserId: user.Id,
                    Name: user.FullName,
                    AvatarRef: user.AvatarRef,
                    BorderAssetKey: borderKey,
                    Level: user.Level
                ));
            }

            return result;
        }
    }
}
