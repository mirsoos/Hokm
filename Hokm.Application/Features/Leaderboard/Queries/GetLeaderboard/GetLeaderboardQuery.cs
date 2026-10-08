using ErrorOr;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.Leaderboard.Queries.GetLeaderboard
{
    public record GetLeaderboardQuery(
            Guid UserId,
            LeaderboardType Type
        ) : IRequest<ErrorOr<LeaderboardResultDto>>;

    public record LeaderboardEntryDto(
        int Rank,
        Guid UserId,
        string Name,
        int AvatarRef,
        string? BorderAssetKey,
        int Level
    );

    public record LeaderboardResultDto(
        List<LeaderboardEntryDto> Entries,
        int MyRank,
        bool MyRankInTop
    );
}
