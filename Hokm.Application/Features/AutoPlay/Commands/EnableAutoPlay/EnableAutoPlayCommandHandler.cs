using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.EnableAutoPlay
{
    public class EnableAutoPlayCommandHandler : IRequestHandler<EnableAutoPlayCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly GameTimerManager _timerManager;

        public EnableAutoPlayCommandHandler(IGameRepository gameRepository, GameTimerManager timerManager)
        {
            _gameRepository = gameRepository;
            _timerManager = timerManager;
        }

        public async Task<Unit> Handle(EnableAutoPlayCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player != null)
            {
                player.EnableAutoPlay(); // فعال کردن حالت اتوپلی در دیتابیس
                await _gameRepository.UpdateAsync(game, cancellationToken);

                // اگر نوبت خودِ این بازیکن قطع شده بود، تایمر او را لغو کن و تایمر جدید ۱ ثانیه‌ای برای بات استارت بزن
                if (game.GetCurrentTurnPlayerId() == request.PlayerId)
                {
                    _timerManager.StartTimer(game.Id, request.PlayerId, 1.0);
                }
            }
            return Unit.Value;
        }
    }
}
