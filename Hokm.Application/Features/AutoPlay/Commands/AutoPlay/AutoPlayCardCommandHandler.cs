using Hokm.Application.Features.PlayCard.Commands;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Bot;
using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.AutoPlay
{

    public class AutoPlayCardCommandHandler : IRequestHandler<AutoPlayCardCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;

        public AutoPlayCardCommandHandler(IGameRepository gameRepository, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(AutoPlayCardCommand request, CancellationToken cancellationToken)
        {
            // ۱. خواندن وضعیت بازی از دیتابیس
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            // ۲. بررسی امنیتی: اگر به هر دلیلی نوبت بازی تغییر کرده بود، عملیات را لغو کن
            if (game.GetCurrentTurnPlayerId() != request.PlayerId)
                return Unit.Value;

            // ۳. فعال کردن حالت اتوپلی برای بازیکن (چون تایم‌اوت شده یا قطع شده است)
            var player = game.Players.First(p => p.Id == request.PlayerId);
            player.EnableAutoPlay();
            await _gameRepository.UpdateAsync(game, cancellationToken);

            // ۴. دریافت کارت پیشنهادی ربات باهوشمان
            var chosenCard = HokmBot.DecideCardToPlay(game, request.PlayerId);
            if (chosenCard == null) return Unit.Value;

            // ۵. اجرای دستور بازی کردن کارت (استفاده مجدد از کدی که خودتان نوشته‌اید)
            var playCardCmd = new PlayCardCommand
            {
                GameId = game.Id,
                PlayerId = request.PlayerId,
                Suit = chosenCard.Suit,
                Rank = chosenCard.Rank
            };

            // ارسال دستور به هندلر شما برای ثبت کارت، ارسال رویدادها به فلاتر و تعیین نوبت بعدی
            await _mediator.Send(playCardCmd, cancellationToken);

            return Unit.Value;
        }
    }
}
