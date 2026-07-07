using Hokm.Application.Constants;
using Hokm.Application.DTOs;
using Hokm.Application.DTOs.GameSnapshot;
using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.PlayCard.Commands
{
    public class PlayCardCommandHandler : IRequestHandler<PlayCardCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;
        private readonly GameTimerManager _timerManager;

        public PlayCardCommandHandler(IGameRepository gameRepository, IMediator mediator, GameTimerManager timerManager)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
            _timerManager = timerManager;
        }

        public async Task<Unit> Handle(PlayCardCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

            if (currentGame == null)
                throw new ArgumentNullException(nameof(request.GameId), "Game not found.");

            _timerManager.CancelTimer(currentGame.Id);

            var card = new Card(request.Suit, request.Rank);

            currentGame.PlayCard(request.PlayerId, card);

            await _gameRepository.UpdateAsync(currentGame, cancellationToken);

            Guid? nextPlayerId = null;
            if (currentGame.Status == GameStatus.Playing)
            {
                nextPlayerId = currentGame.GetCurrentTurnPlayerId();

                if (nextPlayerId.HasValue)
                {
                    var nextPlayer = currentGame.Players.First(p => p.Id == nextPlayerId.Value);

                    double timeoutSeconds = nextPlayer.IsAutoPlay
                        ? GameConstants.BotTurnTimeoutSeconds
                        : GameConstants.HumanTurnTimeoutSeconds;

                    _timerManager.StartTimer(currentGame.Id, nextPlayerId.Value, timeoutSeconds);
                }
            }

            await _mediator.Publish(new GameEventNotification(
                request.GameId,
                "card_played",
                JsonSerializer.Serialize(new
                {
                    PlayerId = request.PlayerId.ToString(),
                    Suit = request.Suit.ToString(),
                    Rank = request.Rank.ToString(),
                    NextTurnPlayerId = nextPlayerId?.ToString()
                }
            )), cancellationToken);

            if (currentGame.CurrentRoundIndex.HasValue && currentGame.Rounds.Count > currentGame.CurrentRoundIndex.Value)
            {
                var activeRound = currentGame.Rounds[currentGame.CurrentRoundIndex.Value];

                foreach (var player in currentGame.Players)
                {
                    if (activeRound.PlayerHands.TryGetValue(player.Id, out var hand))
                    {
                        var handDto = hand.Select(c => new CardDto
                        {
                            Suit = c.Suit.ToString(),
                            Rank = c.Rank.ToString(),
                            IsPlayable = currentGame.IsCardPlayable(player.Id, c)
                        }).ToList();

                        await _mediator.Publish(new PlayerGameEventNotification(
                            request.GameId,
                            player.Id,
                            "your_hand_updated",
                            JsonSerializer.Serialize(new { Cards = handDto })
                        ), cancellationToken);
                    }
                }
            }

            if (currentGame.LastTrickWinnerPlayerId.HasValue)
            {
                int redScore = currentGame.Teams.Count > 0 ? currentGame.Teams[0].TotalScore : 0;
                int blueScore = currentGame.Teams.Count > 1 ? currentGame.Teams[1].TotalScore : 0;

                int redTricks = 0;
                int blueTricks = 0;

                if (currentGame.CurrentRoundIndex.HasValue && currentGame.Rounds.Count > currentGame.CurrentRoundIndex.Value)
                {
                    var activeRound = currentGame.Rounds[currentGame.CurrentRoundIndex.Value];
                    var redTeam = currentGame.Teams.Count > 0 ? currentGame.Teams[0] : null;
                    var blueTeam = currentGame.Teams.Count > 1 ? currentGame.Teams[1] : null;

                    if (redTeam != null)
                        redTricks = activeRound.Tricks.Count(t => t.IsComplete && t.WinnerPlayerId.HasValue && redTeam.PlayerIds.Contains(t.WinnerPlayerId.Value));
                    if (blueTeam != null)
                        blueTricks = activeRound.Tricks.Count(t => t.IsComplete && t.WinnerPlayerId.HasValue && blueTeam.PlayerIds.Contains(t.WinnerPlayerId.Value));
                }

                Guid? nextTurn = null;
                if (currentGame.Status == GameStatus.Playing)
                    nextTurn = currentGame.GetCurrentTurnPlayerId();

                await _mediator.Publish(new GameEventNotification(
                    request.GameId,
                    "trick_completed",
                    JsonSerializer.Serialize(new
                    {
                        WinnerPlayerId = currentGame.LastTrickWinnerPlayerId.Value.ToString(),
                        RedTeamScore = redScore,
                        BlueTeamScore = blueScore,
                        RedTeamTricks = redTricks,
                        BlueTeamTricks = blueTricks,
                        NextTurnPlayerId = nextTurn?.ToString()
                    }
                )), cancellationToken);
            }

            if (currentGame.Status == GameStatus.RoundFinished)
            {
                var winningTeamId = currentGame.Teams.OrderByDescending(t => t.TotalScore).First().Id;

                var roundScores = currentGame.Teams.Select(t => new TeamScoreDto
                {
                    TeamId = t.Id,
                    TotalScore = t.TotalScore
                }).ToList();

                await _mediator.Publish(new GameEventNotification(
                    request.GameId,
                    "round_finished",
                    JsonSerializer.Serialize(new
                    {
                        WinnerTeamId = winningTeamId.ToString(),
                        Scores = roundScores
                    }
                )), cancellationToken);

                var newDealtCards = currentGame.StartNextRound();
                await _gameRepository.UpdateAsync(currentGame, cancellationToken);

                var newActiveRound = currentGame.Rounds[currentGame.CurrentRoundIndex!.Value];
                var dealer = currentGame.Players.First(x => x.Id == newActiveRound.DealerId);
                var hakemSide = currentGame.GetRightSideOf(dealer.PlayerSide);
                var newHakemId = currentGame.Players.First(x => x.PlayerSide == hakemSide).Id;

                var newHakem = currentGame.Players.First(p => p.Id == newHakemId);
                double timeoutSeconds = newHakem.IsAutoPlay
                    ? GameConstants.BotTurnTimeoutSeconds
                    : GameConstants.HumanTurnTimeoutSeconds;

                _timerManager.StartTimer(currentGame.Id, newHakemId, timeoutSeconds, isTrumpSelection: true);

                foreach (var player in currentGame.Players)
                {
                    if (newDealtCards.TryGetValue(player.Id, out var newHand))
                    {
                        var handDto = newHand.Select(c => new CardDto
                        {
                            Suit = c.Suit.ToString(),
                            Rank = c.Rank.ToString(),
                            IsPlayable = true
                        }).ToList();

                        await _mediator.Publish(new PlayerGameEventNotification(
                            request.GameId,
                            player.Id,
                            "your_cards_dealt",
                            JsonSerializer.Serialize(new
                            {
                                IsInitialDeal = true,
                                Cards = handDto,
                                HakemPlayerId = newHakemId.ToString()
                            })
                        ), cancellationToken);
                    }
                }
            }

            if (currentGame.Status == GameStatus.Finished)
            {
                var winnerTeam = currentGame.Teams.FirstOrDefault(t => t.TotalScore >= 7);

                await _mediator.Publish(new GameEventNotification(
                    request.GameId,
                    "game_finished",
                    JsonSerializer.Serialize(new GameFinishedEvent
                    {
                        WinnerTeamId = winnerTeam?.Id,
                        FinalScores = currentGame.Teams.Select(t => new TeamScoreDto
                        {
                            TeamId = t.Id,
                            TotalScore = t.TotalScore
                        }).ToList()
                    }
                )), cancellationToken);
            }

            return Unit.Value;
        }
    }
}