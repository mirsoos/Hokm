using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.ResumeControl
{
    public class ResumeControlCommandHandler : IRequestHandler<ResumeControlCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly GameTimerManager _timerManager;

        public ResumeControlCommandHandler(IGameRepository gameRepository, GameTimerManager timerManager)
        {
            _gameRepository = gameRepository;
            _timerManager = timerManager;
        }

        public async Task<Unit> Handle(ResumeControlCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player != null)
            {
                player.DisableAutoPlay(); // غیرفعال کردن اتوپلی در دیتابیس
                await _gameRepository.UpdateAsync(game, cancellationToken);

                // اگر در همین لحظه نوبت خودِ بازیکن است، تایمر او را ریستارت کن و ۲۰ ثانیه کامل به او وقت بده
                if (game.GetCurrentTurnPlayerId() == request.PlayerId)
                {
                    _timerManager.StartTimer(game.Id, request.PlayerId, 20.0);
                }
            }
            return Unit.Value;
        }
    }
}
