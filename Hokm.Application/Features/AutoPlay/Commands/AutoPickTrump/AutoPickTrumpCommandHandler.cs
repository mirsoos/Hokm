using Hokm.Application.Features.PickTrump.Commands;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.AutoPickTrump
{
    public class AutoPickTrumpCommandHandler : IRequestHandler<AutoPickTrumpCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;

        public AutoPickTrumpCommandHandler(IGameRepository gameRepository, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(AutoPickTrumpCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            // فعال کردن وضعیت اتوپلی حاکم به دلیل عدم تصمیم‌گیری به موقع
            var hakem = game.Players.First(p => p.Id == request.HakemId);
            hakem.EnableAutoPlay();
            await _gameRepository.UpdateAsync(game, cancellationToken);

            var round = game.Rounds[game.CurrentRoundIndex!.Value];
            var firstFiveCards = round.PlayerHands[request.HakemId];

            // منطق انتخاب حکم: شمارش کارت‌های هم‌خال و انتخاب خالی که بیشترین تعداد را دارد
            var bestSuit = firstFiveCards
                .GroupBy(c => c.Suit)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Max(c => (int)c.Rank)) // اگر تعداد برابر بود، خالی با برگ بزرگتر
                .First()
                .Key;

            // اجرای دستور ثبت حکم شما با استفاده از حکم انتخاب شده توسط ربات
            var pickTrumpCmd = new PickTrumpCommand
            {
                GameId = game.Id,
                DealerId = round.DealerId, // دیلر راند فعلی
                TrumpSuit = bestSuit
            };

            await _mediator.Send(pickTrumpCmd, cancellationToken);

            return Unit.Value;
        }
    }
}
