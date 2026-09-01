using Hokm.Application.Constants;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.StartTrumpSelectionTimer
{
    public class StartTrumpSelectionTimerCommandHandler : IRequestHandler<StartTrumpSelectionTimerCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly GameTimerManager _timerManager;

        public StartTrumpSelectionTimerCommandHandler(IGameRepository gameRepository, GameTimerManager timerManager)
        {
            _gameRepository = gameRepository;
            _timerManager = timerManager;
        }

        public async Task<Unit> Handle(StartTrumpSelectionTimerCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            var hakem = game.Players.FirstOrDefault(p => p.Id == request.HakemId);
            if (hakem != null)
            {
                double timeoutSeconds = hakem.IsAutoPlay
                    ? GameConstants.BotTurnTimeoutSeconds
                    : GameConstants.HumanTurnTimeoutSeconds;

                await _timerManager.StartTimer(game.Id, hakem.Id, timeoutSeconds, isTrumpSelection: true);
            }

            return Unit.Value;
        }
    }
}