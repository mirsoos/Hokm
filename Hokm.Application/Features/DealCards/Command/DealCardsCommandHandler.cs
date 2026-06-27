using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Contracts;
using Hokm.Application.Realtime.Mappers;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.DealCards.Command
{
    public class DealCardsCommandHandler : IRequestHandler<DealCardsCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;
        public DealCardsCommandHandler(IGameRepository gameRepository, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }
        public async Task<Unit> Handle(DealCardsCommand request,CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId,cancellationToken);
            if (currentGame == null)
                throw new ArgumentNullException(nameof(request.GameId),"Game not found.");

            var dealtCards = currentGame.StartRoundAndDeal(request.DealerId);

            await _gameRepository.UpdateAsync(currentGame,cancellationToken);

            foreach (var kv in dealtCards)
            {
                var playerId = kv.Key;
                var cards = kv.Value;
                await _mediator.Publish(new PlayerGameEventNotification(
                        request.GameId,
                        playerId,
                        "your_cards_dealt",
                        JsonSerializer.Serialize(new YourCardsDealtEvent
                            {
                                IsInitialDeal = true,

                                Cards = cards
                                    .Select(RealtimeMapper.ToDto)
                                    .ToList()
                            }
                        )),cancellationToken);
            }

            return Unit.Value;
        }
    }
}
