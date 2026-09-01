using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.AutoPlay.Commands.EnableAutoPlay
{
    public class EnableAutoPlayCommandHandler : IRequestHandler<EnableAutoPlayCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly GameTimerManager _timerManager;
        private readonly IMediator _mediator;

        public EnableAutoPlayCommandHandler(
            IGameRepository gameRepository,
            GameTimerManager timerManager,
            IMediator mediator)
        {
            _gameRepository = gameRepository;
            _timerManager = timerManager;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(EnableAutoPlayCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player != null)
            {
                player.EnableAutoPlay();
                await _gameRepository.UpdateAsync(game, cancellationToken);

                await _mediator.Publish(new GameEventNotification(
                    game.Id,
                    "player_status_changed",
                    JsonSerializer.Serialize(new { PlayerId = request.PlayerId.ToString(), IsOnline = false, IsAutoPlay = true })
                ), cancellationToken);

                bool isTrumpPhase = game.Status == GameStatus.WaitingForTeams ||
                                    game.Status == GameStatus.TeamsReady ||
                                    (game.CurrentRoundIndex.HasValue && !game.Rounds[game.CurrentRoundIndex.Value].TrumpSuit.HasValue);

                if (isTrumpPhase)
                {
                    if (game.CurrentRoundIndex.HasValue)
                    {
                        var activeRound = game.Rounds[game.CurrentRoundIndex.Value];
                        var dealer = game.Players.First(x => x.Id == activeRound.DealerId);
                        var hakemSide = game.GetRightSideOf(dealer.PlayerSide);
                        var hakem = game.Players.First(x => x.PlayerSide == hakemSide);

                        if (hakem.Id == request.PlayerId)
                        {
                            await _timerManager.StartTimer(game.Id, request.PlayerId, 1.0, isTrumpSelection: true);
                        }
                    }
                }
                else if (game.Status == GameStatus.Playing && game.GetCurrentTurnPlayerId() == request.PlayerId)
                {
                    await _timerManager.StartTimer(game.Id, request.PlayerId, 1.0, isTrumpSelection: false);
                }
            }
            return Unit.Value;
        }
    }
}