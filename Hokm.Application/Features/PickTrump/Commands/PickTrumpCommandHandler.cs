using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Contracts;
using Hokm.Application.Realtime.Mappers;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.PickTrump.Commands
{
    public class PickTrumpCommandHandler : IRequestHandler<PickTrumpCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;

        public PickTrumpCommandHandler(IGameRepository gameRepository, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(PickTrumpCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (currentGame == null)
                throw new ArgumentNullException(nameof(request.GameId), "Game not found.");

            // اجرای منطق تعیین حکم بر اساس شناسه حاکم و توزیع کارت‌های باقی‌مانده (حجم کارت‌های دست به ۱۳ می‌رسد)
            var remainingCards = currentGame.SetTrumpForCurrentRound(request.TrumpSuit, request.DealerId);

            await _gameRepository.UpdateAsync(currentGame, cancellationToken);

            var turnPlayerId = currentGame.GetCurrentTurnPlayerId();

            // ۱. ارسال رویداد انتخاب حکم برای همه بازیکنان
            await _mediator.Publish(new GameEventNotification(
                request.GameId,
                "trump_picked",
                JsonSerializer.Serialize(new
                {
                    TrumpSuit = request.TrumpSuit.ToString(),
                    CurrentTurnPlayerId = turnPlayerId?.ToString()
                })
            ), cancellationToken);

            // ۲. ارسال کارت‌های باقی‌مانده به صورت مجزا برای هر بازیکن (برای سیستم‌های همگام‌ساز قدیمی)
            foreach (var kv in remainingCards)
            {
                var playerId = kv.Key;
                var cards = kv.Value;

                await _mediator.Publish(new PlayerGameEventNotification(
                    request.GameId,
                    playerId,
                    "your_cards_dealt",
                    JsonSerializer.Serialize(new YourCardsDealtEvent
                    {
                        IsInitialDeal = false,
                        Cards = cards.Select(RealtimeMapper.ToDto).ToList()
                    })
                ), cancellationToken);
            }

            // ۳. ارسال دست‌های کامل ۱۳ کارتی به همراه وضعیت IsPlayable جدید برای هر ۴ بازیکن (هسته Dumb Client)
            if (currentGame.CurrentRoundIndex.HasValue && currentGame.Rounds.Count > currentGame.CurrentRoundIndex.Value)
            {
                var activeRound = currentGame.Rounds[currentGame.CurrentRoundIndex.Value];

                foreach (var player in currentGame.Players)
                {
                    if (activeRound.PlayerHands.TryGetValue(player.Id, out var hand))
                    {
                        var handDto = hand.Select(c => new DTOs.GameSnapshot.CardDto
                        {
                            Suit = c.Suit.ToString(),
                            Rank = c.Rank.ToString(),
                            // سرور تعیین می‌کند چه کارت‌هایی در آغاز بازی برای بازیکن هدف مجاز هستند (که برای حاکم فعال و برای دیگران غیرفعال خواهد بود)
                            IsPlayable = currentGame.IsCardPlayable(player.Id, c)
                        }).ToList();

                        await _mediator.Publish(new PlayerGameEventNotification(
                            request.GameId,
                            player.Id,
                            "your_hand_updated",
                            JsonSerializer.Serialize(new { Cards = handDto }) // بسته‌بندی شد
                        ), cancellationToken);
                    }
                }
            }

            return Unit.Value;
        }
    }
}