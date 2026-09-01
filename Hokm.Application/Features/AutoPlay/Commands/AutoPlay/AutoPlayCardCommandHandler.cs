using Hokm.Application.Events;
using Hokm.Application.Features.PlayCard.Commands;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Bot;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.AutoPlay.Commands.AutoPlay
{
    public class AutoPlayCardCommandHandler : IRequestHandler<AutoPlayCardCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;

        public AutoPlayCardCommandHandler(
            IGameRepository gameRepository,
            IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(AutoPlayCardCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            if (game.GetCurrentTurnPlayerId() != request.PlayerId)
                return Unit.Value;

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player == null) return Unit.Value;

            // اگر کاربر انسان بوده و تایم‌اوت شده، او را اتوپلی می‌کنیم و به فرانت اطلاع می‌دهیم
            if (!player.IsAutoPlay)
            {
                player.EnableAutoPlay();
                await _gameRepository.UpdateAsync(game, cancellationToken);

                await _mediator.Publish(new GameEventNotification(
                    game.Id,
                    "player_status_changed",
                    JsonSerializer.Serialize(new
                    {
                        PlayerId = request.PlayerId.ToString(),
                        IsOnline = true,
                        IsAutoPlay = true
                    })
                ), cancellationToken);
            }

            // انتخاب کارت توسط الگوریتم ربات
            var chosenCard = HokmBot.DecideCardToPlay(game, request.PlayerId);

            // Fallback: اگر الگوریتم کارت پیدا نکرد، اولین کارت مجاز دست را بردار تا بازی متوقف نشود
            if (chosenCard == null && game.CurrentRoundIndex.HasValue)
            {
                var round = game.Rounds[game.CurrentRoundIndex.Value];
                if (round.PlayerHands.TryGetValue(request.PlayerId, out var hand))
                {
                    chosenCard = hand.FirstOrDefault(c => game.IsCardPlayable(request.PlayerId, c)) ?? hand.FirstOrDefault();
                }
            }

            if (chosenCard == null) return Unit.Value;

            var playCardCmd = new PlayCardCommand
            {
                GameId = game.Id,
                PlayerId = request.PlayerId,
                Suit = chosenCard.Suit,
                Rank = chosenCard.Rank
            };

            await _mediator.Send(playCardCmd, cancellationToken);

            return Unit.Value;
        }
    }
}