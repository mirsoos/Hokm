using ErrorOr;
using MediatR;

namespace Hokm.Application.Features.profile.Queries.GetStats
{
	public record GetStatsQuery(Guid UserId) : IRequest<ErrorOr<GetStatsResponse>>;

	public record GetStatsResponse(
		int Wins,
		int Loses,
		int TotalGames,
		double WinRate,
		int Score,
		int Level,
		string Rank
	);
}
