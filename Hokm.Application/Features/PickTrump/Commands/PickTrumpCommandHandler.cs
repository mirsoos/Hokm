using Hokm.Application.Constants;
using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Contracts;
using Hokm.Application.Realtime.Execution;
using Hokm.Application.Realtime.Mappers;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.PickTrump.Commands
{
    public class PickTrumpCommandHandler : IRequestHandler<PickTrumpCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;
        private readonly GameTimerManager _timerManager;

        public PickTrumpCommandHandler(
            IGameRepository gameRepository,
            IMediator mediator,
            GameTimerManager timerManager)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
            _timerManager = timerManager;
        }

        public async Task<Unit> Handle(PickTrumpCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (currentGame == null)
                throw new ArgumentNullException(nameof(request.GameId), "Game not found.");

            _timerManager.CancelTimer(currentGame.Id);

            var remainingCards = currentGame.SetTrumpForCurrentRound(request.TrumpSuit, request.DealerId);

            currentGame.StartPlaying();

            await _gameRepository.UpdateAsync(currentGame, cancellationToken);

            var turnPlayerId = currentGame.GetCurrentTurnPlayerId();

            // اعلام حکم به همه
            await _mediator.Publish(new GameEventNotification(
                request.GameId,
                "trump_picked",
                JsonSerializer.Serialize(new
                {
                    TrumpSuit = request.TrumpSuit.ToString(),
                    CurrentTurnPlayerId = turnPlayerId?.ToString()
                })
            ), cancellationToken);

            // ارسال ۸ برگ دست دوم
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

            // ارسال وضعیت کامل دست هر بازیکن
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
                            IsPlayable = currentGame.IsCardPlayable(player.Id, c)
                        }).ToList();

                        await _mediator.Publish(new PlayerGameEventNotification(
                            request.GameId,
                            player.Id,
                            "your_hand_updated",
                            JsonSerializer.Serialize(new { Cards = handDto })
                        ), cancellationToken);
                    }
                }
            }

            // استارت قطعی اولین نوبت بازی (حاکم)
            if (turnPlayerId.HasValue)
            {
                var turnPlayer = currentGame.Players.FirstOrDefault(p => p.Id == turnPlayerId.Value);
                if (turnPlayer != null)
                {
                    if (turnPlayer.IsAutoPlay)
                    {
                        // ۱.۵ ثانیه تأخیر برای دیدن کارت‌های توزیع‌شده در فرانت و انداختن اولین کارت
                        await _timerManager.StartTimer(currentGame.Id, turnPlayerId.Value, 1.5, isTrumpSelection: false);
                    }
                    else
                    {
                        await _timerManager.StartTimer(currentGame.Id, turnPlayerId.Value, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: false);
                    }
                }
            }

            return Unit.Value;
        }
    }
}