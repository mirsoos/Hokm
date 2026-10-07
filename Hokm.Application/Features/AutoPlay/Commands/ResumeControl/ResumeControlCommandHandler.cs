using Hokm.Application.Constants;
using Hokm.Application.DTOs;
using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.AutoPlay.Commands.ResumeControl
{
    public class ResumeControlCommandHandler : IRequestHandler<ResumeControlCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly GameTimerManager _timerManager;
        private readonly IMediator _mediator;

        public ResumeControlCommandHandler(IGameRepository gameRepository, GameTimerManager timerManager, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _timerManager = timerManager;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(ResumeControlCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;


            Console.WriteLine("=== ResumeControl ===");
            Console.WriteLine($"Status = {game.Status}");
            Console.WriteLine($"TargetRounds = {game.TargetRounds}");
            Console.WriteLine($"TableKind = {game.TableKind}");
            Console.WriteLine($"Teams count = {game.Teams.Count}");
            foreach (var t in game.Teams)
            {
                Console.WriteLine($"  Team {t.Id}: Score={t.TotalScore}, Players={t.PlayerIds.Count}");
            }
            Console.WriteLine($"Players count = {game.Players.Count}");
            Console.WriteLine($"====================");

            if (game.Status == GameStatus.Finished)
            {
                // برنده رو از WinnerPlayers پیدا کن (که EndGame قبلاً ست کرده)
                Guid? winnerTeamId = null;
                if (game.WinnerPlayers.Any())
                {
                    var winnerPlayerId = game.WinnerPlayers.First();
                    winnerTeamId = game.Teams
                        .FirstOrDefault(t => t.PlayerIds.Contains(winnerPlayerId))?.Id;
                }

                await _mediator.Publish(new GameEventNotification(
                    game.Id,
                    "game_finished",
                    JsonSerializer.Serialize(new GameFinishedEvent
                    {
                        WinnerTeamId = winnerTeamId,
                        Reward = GameConstants.GetTablePrize(game.TableKind),
                        FinalScores = game.Teams.Select(t => new TeamScoreDto
                        {
                            TeamId = t.Id,
                            TotalScore = t.TotalScore
                        }).ToList()
                    })
                ), cancellationToken);

                return Unit.Value;
            }

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player != null)
            {
                player.DisableAutoPlay();
                await _gameRepository.UpdateAsync(game, cancellationToken);

                await _mediator.Publish(new GameEventNotification(
                    game.Id,
                    "player_status_changed",
                    JsonSerializer.Serialize(new
                    {
                        PlayerId = request.PlayerId.ToString(),
                        IsOnline = true,
                        IsAutoPlay = false
                    })
                ), cancellationToken);

                bool isTrumpPhase = game.Status == GameStatus.WaitingForTrumpSelection;

                if (isTrumpPhase && game.CurrentRoundIndex.HasValue)
                {
                    var activeRound = game.Rounds[game.CurrentRoundIndex.Value];
                    var dealer = game.Players.First(x => x.Id == activeRound.DealerId);
                    var hakemSide = game.GetRightSideOf(dealer.PlayerSide);
                    var hakem = game.Players.First(x => x.PlayerSide == hakemSide);

                    if (hakem.Id == request.PlayerId)
                    {
                        await _timerManager.StartTimer(game.Id, request.PlayerId, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: true);
                    }
                }
                else if (game.Status == GameStatus.Playing && game.GetCurrentTurnPlayerId() == request.PlayerId)
                {
                    await _timerManager.StartTimer(game.Id, request.PlayerId, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: false);
                }
            }
            return Unit.Value;
        }
    }
}