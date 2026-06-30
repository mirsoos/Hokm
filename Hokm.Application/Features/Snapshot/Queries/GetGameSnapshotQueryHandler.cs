using Hokm.Application.DTOs.GameSnapshot;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.Snapshot.Queries
{
    public class GetGameSnapshotQueryHandler : IRequestHandler<GetGameSnapshotQuery, GameSnapshotDto>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IUserRepository _userRepository;

        public GetGameSnapshotQueryHandler(IGameRepository gameRepository,IUserRepository userRepository)
        {
            _gameRepository = gameRepository;
            _userRepository = userRepository;
        }

        public async Task<GameSnapshotDto> Handle(GetGameSnapshotQuery request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null)
            {
                throw new KeyNotFoundException("بازی مورد نظر یافت نشد.");
            }

            var snapshot = new GameSnapshotDto
            {
                GameId = game.Id,
                Status = game.Status.ToString(),
                CurrentTurnPlayerId = game.GetCurrentTurnPlayerId()
            };

            if (game.Teams.Count >= 2)
            {
                snapshot.RedTeamScore = game.Teams[0].TotalScore;
                snapshot.BlueTeamScore = game.Teams[1].TotalScore;
            }

            if (game.CurrentRoundIndex.HasValue && game.Rounds.Count > game.CurrentRoundIndex.Value)
            {
                var activeRound = game.Rounds[game.CurrentRoundIndex.Value];
                snapshot.TrumpSuit = activeRound.TrumpSuit.ToString();

                var redTeam = game.Teams.Count > 0 ? game.Teams[0] : null;
                var blueTeam = game.Teams.Count > 1 ? game.Teams[1] : null;

                if (redTeam != null)
                    snapshot.RedTeamTricks = activeRound.Tricks.Count(t => t.IsComplete && t.WinnerPlayerId.HasValue && redTeam.PlayerIds.Contains(t.WinnerPlayerId.Value));
                if (blueTeam != null)
                    snapshot.BlueTeamTricks = activeRound.Tricks.Count(t => t.IsComplete && t.WinnerPlayerId.HasValue && blueTeam.PlayerIds.Contains(t.WinnerPlayerId.Value));

                if (activeRound.PlayerHands.TryGetValue(request.PlayerId, out var hand))
                {
                    foreach (var card in hand)
                    {
                        snapshot.YourHand.Add(new CardDto
                        {
                            Rank = card.Rank.ToString(),
                            Suit = card.Suit.ToString(),
                            IsPlayable = game.IsCardPlayable(request.PlayerId, card)
                        });
                    }
                }

                var currentTrick = activeRound.Tricks.LastOrDefault(t => !t.IsComplete);
                if (currentTrick != null)
                {
                    foreach (var played in currentTrick.PlayedCards)
                    {
                        snapshot.CurrentTrick.Add(new PlayedCardDto
                        {
                            PlayerId = played.Key,
                            Rank = played.Value.Rank.ToString(),
                            Suit = played.Value.Suit.ToString()
                        });
                    }
                }

                var dealer = game.Players.First(x => x.Id == activeRound.DealerId);
                var hakemSide = game.GetRightSideOf(dealer.PlayerSide);
                var hakem = game.Players.First(x => x.PlayerSide == hakemSide);


                foreach (var player in game.Players)
                {
                    int cardCount = 0;
                    if (activeRound.PlayerHands.TryGetValue(player.Id, out var playerHand))
                    {
                        cardCount = playerHand.Count;
                    }
                    var user = await _userRepository.GetByIdAsync(player.Id, cancellationToken);

                    snapshot.Players.Add(new PlayerSnapshotDto
                    {
                        PlayerId = player.Id,
                        Name = player.Name,
                        Side = player.PlayerSide.ToString(),
                        IsHakem = hakem.Id == player.Id,
                        Avatar = user.AvatarRef,
                        CardCount = cardCount,
                        IsAutoPlay = player.IsAutoPlay
                    });
                }
            }
            else
            {
                foreach (var player in game.Players)
                {
                    snapshot.Players.Add(new PlayerSnapshotDto
                    {
                        PlayerId = player.Id,
                        Name = player.Name,
                        Side = player.PlayerSide.ToString(),
                        IsHakem = false,
                        Avatar = 1,
                        CardCount = 0
                    });
                }
            }

            return snapshot;
        }
    }
}