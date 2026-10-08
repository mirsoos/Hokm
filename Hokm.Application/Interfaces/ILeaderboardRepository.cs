namespace Hokm.Application.Interfaces
{
    public interface ILeaderboardRepository
    {
        Task<List<Guid>> GetTopWeeklyWinnerIdsAsync(int days, int limit, CancellationToken ct);
        Task<List<Guid>> GetTopMonthlyWinnerIdsAsync(int days, int limit, CancellationToken ct);
        Task<List<Guid>> GetTopWinrateIdsAsync(int minGames, int limit, CancellationToken ct);

        Task<int> GetWeeklyRankAsync(Guid userId, int days, CancellationToken ct);
        Task<int> GetMonthlyRankAsync(Guid userId, int days, CancellationToken ct);
        Task<int> GetWinrateRankAsync(Guid userId, int minGames, CancellationToken ct);
    }
}
