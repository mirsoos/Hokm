using ErrorOr;
using MediatR;

namespace Hokm.Application.Features.GetPlayerProfile.Queries
{
    public record GetPlayerProfileQuery(
            Guid GameId,
            Guid RequesterId,
            Guid TargetPlayerId
        ) : IRequest<ErrorOr<GetPlayerProfileResponse>>;

    public record GetPlayerProfileResponse(
        Guid PlayerId,
        string Name,
        string Avatar,
        string? Border,
        int Level,
        long Coins,
        bool IsVip,
        int WonHands,
        int LostHands
    );
}
