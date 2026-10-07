using ErrorOr;
using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using MediatR;

namespace Hokm.Application.Features.TrickDetails.Queries.GetTrickDetails
{
    public class GetTrickDetailsQueryHandler
           : IRequestHandler<GetTrickDetailsQuery, ErrorOr<GetTrickDetailsResponse>>
    {
        private readonly IGameRepository _gameRepository;

        public GetTrickDetailsQueryHandler(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }

        public async Task<ErrorOr<GetTrickDetailsResponse>> Handle(
            GetTrickDetailsQuery request,
            CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null)
                return Error.NotFound("Game.NotFound", "Game not found.");

            var requesterPlayer = game.Players.FirstOrDefault(p => p.Id == request.RequesterId);
            if (requesterPlayer == null)
                return Error.NotFound("Player.NotFound", "Requester player not found in this game.");

            var requesterTeam = game.Teams.FirstOrDefault(t => t.PlayerIds.Contains(requesterPlayer.Id));
            if (requesterTeam == null)
                return Error.Failure("Team.NotFound", "Requester team not found.");

            var otherTeam = game.Teams.FirstOrDefault(t => t.Id != requesterTeam.Id);
            if (otherTeam == null)
                return Error.Failure("Team.NotFound", "Opponent team not found.");

            Team? targetTeam = request.TeamId switch
            {
                "team1" => requesterTeam,
                "team2" => otherTeam,
                _ => null
            };

            if (targetTeam == null)
                return Error.Validation("Team.Invalid", "Invalid team id.");

            if (!game.CurrentRoundIndex.HasValue)
                return Error.NotFound("Round.NotFound", "No active round.");

            var activeRound = game.Rounds[game.CurrentRoundIndex.Value];

            var targetTricks = activeRound.Tricks
                .Where(t => t.IsComplete
                         && t.WinnerPlayerId.HasValue
                         && targetTeam.PlayerIds.Contains(t.WinnerPlayerId.Value))
                .ToList();

            if (request.TrickIndex < 0 || request.TrickIndex >= targetTricks.Count)
                return Error.NotFound("Trick.NotFound", "Trick index out of range.");

            var targetTrick = targetTricks[request.TrickIndex];

            var cards = new List<TrickCardInfo>();
            foreach (var pid in targetTrick.PlayerOrder)
            {
                if (targetTrick.PlayedCards.TryGetValue(pid, out var card))
                {
                    cards.Add(new TrickCardInfo(
                        Suit: card.Suit.ToString(),
                        Rank: card.Rank.ToString(),
                        PlayerId: pid
                    ));
                }
            }

            return new GetTrickDetailsResponse(cards);
        }
    }
}
