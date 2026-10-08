using Hokm.Application.Constants;
using Hokm.Application.DTOs;
using Hokm.Application.DTOs.GameSnapshot;
using Hokm.Application.Events;
using Hokm.Application.Features.StartNextRound.Commands;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Hokm.Application.Features.PlayCard.Commands
{
    public class PlayCardCommandHandler : IRequestHandler<PlayCardCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;
        private readonly GameTimerManager _timerManager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GameExecutionCoordinator _coordinator;

        public PlayCardCommandHandler(
            IGameRepository gameRepository,
            IMediator mediator,
            GameTimerManager timerManager,
            IServiceScopeFactory scopeFactory,
            GameExecutionCoordinator coordinator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
            _timerManager = timerManager;
            _scopeFactory = scopeFactory;
            _coordinator = coordinator;
        }

        public async Task<Unit> Handle(PlayCardCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

            if (currentGame == null)
                throw new ArgumentNullException(nameof(request.GameId), "Game not found.");

            _timerManager.CancelTimer(currentGame.Id);

            var card = new Card(request.Suit, request.Rank);

            bool isCut = false;
            if (currentGame.CurrentRoundIndex.HasValue && currentGame.Rounds.Count > currentGame.CurrentRoundIndex.Value)
            {
                var activeRound = currentGame.Rounds[currentGame.CurrentRoundIndex.Value];
                var currentTrick = activeRound.Tricks.LastOrDefault(t => !t.IsComplete);

                if (currentTrick != null && activeRound.TrumpSuit.HasValue && currentTrick.LedSuit.HasValue)
                {
                    if (currentTrick.LedSuit.Value != activeRound.TrumpSuit.Value && card.Suit == activeRound.TrumpSuit.Value)
                    {
                        isCut = true;
                    }
                }
            }

            currentGame.PlayCard(request.PlayerId, card);

            await _gameRepository.UpdateAsync(currentGame, cancellationToken);

            Guid? nextPlayerId = null;
            if (currentGame.Status == GameStatus.Playing)
            {
                nextPlayerId = currentGame.GetCurrentTurnPlayerId();
            }

            await _mediator.Publish(new GameEventNotification(
                request.GameId,
                "card_played",
                JsonSerializer.Serialize(new
                {
                    PlayerId = request.PlayerId.ToString(),
                    Suit = request.Suit.ToString(),
                    Rank = request.Rank.ToString(),
                    IsCut = isCut,
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

            bool isTrickFinished = currentGame.LastTrickWinnerPlayerId.HasValue;
            if (isTrickFinished)
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
                        NextTurnPlayerId = nextPlayerId?.ToString()
                    }
                )), cancellationToken);
            }

            if (currentGame.Status == GameStatus.Playing && nextPlayerId.HasValue)
            {
                var nextPlayer = currentGame.Players.FirstOrDefault(p => p.Id == nextPlayerId.Value);
                if (nextPlayer != null)
                {
                    if (nextPlayer.IsAutoPlay)
                    {
                        double delaySeconds = isTrickFinished ? 2.0 : 1.2;
                        await _timerManager.StartTimer(currentGame.Id, nextPlayer.Id, delaySeconds, isTrumpSelection: false);
                    }
                    else
                    {
                        await _timerManager.StartTimer(currentGame.Id, nextPlayer.Id, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: false);
                    }
                }
            }

            if (currentGame.Status == GameStatus.RoundFinished)
            {
                var winningTeamId = currentGame.LastRoundWinnerTeamId
                    ?? currentGame.Teams.OrderByDescending(t => t.TotalScore).First().Id;

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

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(5000);
                        var startNextRoundCmd = new StartNextRoundCommand { GameId = request.GameId };
                        await _coordinator.ExecuteAsync(request.GameId, startNextRoundCmd, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ خطا در ارسال دستور شروع راند بعدی به صف: {ex.Message}");
                    }
                });
            }

            if (currentGame.Status == GameStatus.Finished)
            {
                var winnerTeam = currentGame.Teams.FirstOrDefault(t => t.TotalScore >= currentGame.TargetRounds);
                int coinReward = GameConstants.GetTablePrize(currentGame.TableKind);
                int winXp = GameConstants.GetTableWinXp(currentGame.TableKind);
                int lossXp = GameConstants.GetTableLossXp(currentGame.TableKind);

                if (winnerTeam != null)
                {
                    var winningPlayerIds = winnerTeam.PlayerIds.ToList();
                    var losingTeam = currentGame.Teams.FirstOrDefault(t => t.Id != winnerTeam.Id);
                    var losingPlayerIds = losingTeam?.PlayerIds.ToList() ?? new List<Guid>();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                            foreach (var winnerId in winningPlayerIds)
                            {
                                var user = await userRepo.GetByIdAsync(winnerId, CancellationToken.None);
                                if (user != null && !user.IsBot)
                                {
                                    if (coinReward > 0) user.AddCoins(coinReward);
                                    user.RecordWin(winXp);
                                    await userRepo.UpdateAsync(user, CancellationToken.None);
                                }
                            }

                            foreach (var loserId in losingPlayerIds)
                            {
                                var user = await userRepo.GetByIdAsync(loserId, CancellationToken.None);
                                if (user != null && !user.IsBot)
                                {
                                    user.RecordLoss(lossXp);
                                    await userRepo.UpdateAsync(user, CancellationToken.None);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing end game user stats: {ex.Message}");
                        }
                        finally
                        {
                            await _coordinator.RemoveWorkerAsync(request.GameId);
                        }
                    });
                }

                await _mediator.Publish(new GameEventNotification(
                    request.GameId,
                    "game_finished",
                    JsonSerializer.Serialize(new GameFinishedEvent
                    {
                        WinnerTeamId = winnerTeam?.Id,
                        Reward = coinReward,
                        FinalScores = currentGame.Teams.Select(t => new TeamScoreDto
                        {
                            TeamId = t.Id,
                            TotalScore = t.TotalScore
                        }).ToList()
                    })
                ), cancellationToken);
            }

            return Unit.Value;
        }
    }
}