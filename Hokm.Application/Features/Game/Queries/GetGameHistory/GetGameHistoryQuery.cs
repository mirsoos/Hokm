using ErrorOr;
using MediatR;

namespace Hokm.Application.Features.Game.Queries.GetGameHistory
{
    public record GetGameHistoryQuery(Guid UserId,int Take) : IRequest<ErrorOr<List<GameHistoryItem>?>>;

    public record GameHistoryItem(
        Guid GameId,
        string Date,
        bool IsWin,
        int ScoreChange,
        string OpponentName
    );
}
