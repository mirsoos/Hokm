using ErrorOr;
using MediatR;

namespace Hokm.Application.Features.TrickDetails.Queries.GetTrickDetails
{
    public record GetTrickDetailsQuery(
            Guid GameId,
            Guid RequesterId,
            int TrickIndex,
            string TeamId
        ) : IRequest<ErrorOr<GetTrickDetailsResponse>>;

    public record TrickCardInfo(
        string Suit,
        string Rank,
        Guid PlayerId
    );

    public record GetTrickDetailsResponse(
        List<TrickCardInfo> Cards
    );
}
