using Hokm.Application.Constants;
using Hokm.Application.DTOs;
using Hokm.Application.DTOs.GameSnapshot;
using Hokm.Application.Events;
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

        public PlayCardCommandHandler(
            IGameRepository gameRepository,
            IMediator mediator,
            GameTimerManager timerManager,
            IServiceScopeFactory scopeFactory)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
            _timerManager = timerManager;
            _scopeFactory = scopeFactory;
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

            // محاسبه نوبت نفر بعدی
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

            // به‌روزرسانی دست بازیکنان
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

            // بررسی پایان دست (Trick)
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

            // مدیریت نوبت بعدی (موتور محرک بدون بن‌بست)
            if (currentGame.Status == GameStatus.Playing && nextPlayerId.HasValue)
            {
                var nextPlayer = currentGame.Players.FirstOrDefault(p => p.Id == nextPlayerId.Value);
                if (nextPlayer != null)
                {
                    if (nextPlayer.IsAutoPlay)
                    {
                        // اگر ربات یا اتوپلی است: زمان کوتاه همراه با فرصت انیمیشن جمع‌آوری دست
                        double delaySeconds = isTrickFinished ? 2.0 : 1.2;
                        await _timerManager.StartTimer(currentGame.Id, nextPlayer.Id, delaySeconds, isTrumpSelection: false);
                    }
                    else
                    {
                        // اگر انسان است: تایمر مستقیم و استاندارد ارسال به فرانت
                        await _timerManager.StartTimer(currentGame.Id, nextPlayer.Id, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: false);
                    }
                }
            }

            // پایان راند
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

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var scopedGameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
                        var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        await Task.Delay(5000);

                        var bgGame = await scopedGameRepo.GetByIdAsync(request.GameId, CancellationToken.None);
                        if (bgGame == null) return;

                        var newDealtCards = bgGame.StartNextRound();
                        await scopedGameRepo.UpdateAsync(bgGame, CancellationToken.None);

                        var newActiveRound = bgGame.Rounds[bgGame.CurrentRoundIndex!.Value];

                        var newHakemPlayer = bgGame.Players.First(x => x.Id == newActiveRound.HakemId);

                        if (newHakemPlayer.IsAutoPlay)
                        {
                            await _timerManager.StartTimer(bgGame.Id, newHakemPlayer.Id, 1.5, isTrumpSelection: true);
                        }
                        else
                        {
                            await _timerManager.StartTimer(bgGame.Id, newHakemPlayer.Id, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: true);
                        }

                        foreach (var player in bgGame.Players)
                        {
                            if (newDealtCards.TryGetValue(player.Id, out var newHand))
                            {
                                var handDto = newHand.Select(c => new CardDto
                                {
                                    Suit = c.Suit.ToString(),
                                    Rank = c.Rank.ToString(),
                                    IsPlayable = true
                                }).ToList();

                                await scopedMediator.Publish(new PlayerGameEventNotification(
                                    bgGame.Id,
                                    player.Id,
                                    "your_cards_dealt",
                                    JsonSerializer.Serialize(new
                                    {
                                        IsInitialDeal = true,
                                        Cards = handDto,
                                        HakemPlayerId = newHakemPlayer.Id.ToString()
                                    })
                                ), CancellationToken.None);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error starting next round: {ex.Message}");
                    }
                });
            }

            // پایان کل بازی
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