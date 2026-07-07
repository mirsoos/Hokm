using Hokm.Application.Events;
using Hokm.Application.Features.Snapshot.Queries;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
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

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player != null)
            {
                player.DisableAutoPlay();
                await _gameRepository.UpdateAsync(game, cancellationToken);

                if (game.GetCurrentTurnPlayerId() == request.PlayerId)
                {
                    _timerManager.StartTimer(game.Id, request.PlayerId, 20.0);
                }

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

                var snapshotQuery = new GetGameSnapshotQuery { GameId = game.Id, PlayerId = request.PlayerId };
                var snapshot = await _mediator.Send(snapshotQuery, cancellationToken);

                await _mediator.Publish(new PlayerGameEventNotification(
                    game.Id,
                    request.PlayerId,
                    "game_state_updated",
                    JsonSerializer.Serialize(new { GameState = snapshot })
                ), cancellationToken);
            }
            return Unit.Value;
        }
    }
}
