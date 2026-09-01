using Hokm.Application.Constants;
using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.StartPlayingPhase
{
    public class StartPlayingPhaseCommandHandler : IRequestHandler<StartPlayingPhaseCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly GameTimerManager _timerManager;
        private readonly IMediator _mediator;

        public StartPlayingPhaseCommandHandler(IGameRepository gameRepository, GameTimerManager timerManager, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _timerManager = timerManager;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(StartPlayingPhaseCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            game.StartPlaying();
            await _gameRepository.UpdateAsync(game, cancellationToken);

            var firstPlayerId = game.GetCurrentTurnPlayerId()!.Value;

            await _mediator.Publish(new GameEventNotification(
                game.Id,
                "playing_started",
                JsonSerializer.Serialize(new
                {
                    NextTurnPlayerId = firstPlayerId.ToString()
                })
            ), cancellationToken);

            var firstPlayer = game.Players.First(p => p.Id == firstPlayerId);

            double timeoutSeconds = firstPlayer.IsAutoPlay
                ? GameConstants.BotTurnTimeoutSeconds
                : GameConstants.HumanTurnTimeoutSeconds;

            await _timerManager.StartTimer(game.Id, firstPlayerId, timeoutSeconds);

            return Unit.Value;
        }
    }
}
