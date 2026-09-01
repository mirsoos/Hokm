using Hokm.Application.Constants;
using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.ReadyToPlay.Commands
{
    public class ReadyToPlayCommandHandler : IRequestHandler<ReadyToPlayCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly GameTimerManager _timerManager;
        private readonly IMediator _mediator;

        public ReadyToPlayCommandHandler(
            IGameRepository gameRepository,
            GameTimerManager timerManager,
            IMediator mediator)
        {
            _gameRepository = gameRepository;
            _timerManager = timerManager;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(ReadyToPlayCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

            if (currentGame == null)
                throw new ArgumentNullException(nameof(request.GameId), "Game not found.");

            if (currentGame.Status == GameStatus.DealingRemainingCards)
            {
                currentGame.StartPlaying();
                await _gameRepository.UpdateAsync(currentGame, cancellationToken);

                var firstPlayerId = currentGame.GetCurrentTurnPlayerId()!.Value;

                await _mediator.Publish(new GameEventNotification(
                    currentGame.Id,
                    "playing_started",
                    JsonSerializer.Serialize(new
                    {
                        NextTurnPlayerId = firstPlayerId.ToString()
                    })
                ), cancellationToken);

                _timerManager.CancelTimer(currentGame.Id);

                var firstPlayer = currentGame.Players.First(p => p.Id == firstPlayerId);

                double timeoutSeconds = firstPlayer.IsAutoPlay
                    ? GameConstants.BotTurnTimeoutSeconds
                    : GameConstants.HumanTurnTimeoutSeconds;

                await _timerManager.StartTimer(currentGame.Id, firstPlayerId, timeoutSeconds);
            }
            else if (currentGame.Status == GameStatus.Playing)
            {
                Guid? nextPlayerId = currentGame.GetCurrentTurnPlayerId();

                if (nextPlayerId.HasValue)
                {
                    var nextPlayer = currentGame.Players.First(p => p.Id == nextPlayerId.Value);

                    bool isTurnAuthorized = (!nextPlayer.IsAutoPlay && request.PlayerId == nextPlayerId.Value) ||
                                            (nextPlayer.IsAutoPlay);

                    if (isTurnAuthorized)
                    {
                        _timerManager.CancelTimer(currentGame.Id);

                        double timeoutSeconds = nextPlayer.IsAutoPlay
                            ? GameConstants.BotTurnTimeoutSeconds
                            : GameConstants.HumanTurnTimeoutSeconds;

                        await _timerManager.StartTimer(currentGame.Id, nextPlayerId.Value, timeoutSeconds);

                        await _gameRepository.UpdateAsync(currentGame, cancellationToken);
                    }
                }
            }

            return Unit.Value;
        }
    }
}