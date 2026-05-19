using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.PlayCard.Commands
{
    public class PlayCardCommandHandler
    : IRequestHandler<PlayCardCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;

        public PlayCardCommandHandler(IGameRepository gameRepository,IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(PlayCardCommand request,CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);

            if (currentGame == null)
            {
                throw new ArgumentNullException(nameof(request.GameId),"Game not found.");
            }

            var card = new Card(request.Suit, request.Rank);

            currentGame.PlayCard(request.PlayerId, card);

            await _gameRepository.UpdateAsync(currentGame, cancellationToken);

            Guid? nextPlayerId = null;

            if (currentGame.Status == GameStatus.Playing)
            {
                nextPlayerId = currentGame.GetCurrentTurnPlayerId();
            }

            var payload = new
            {
                gameId = request.GameId,
                playerId = request.PlayerId,
                suit = request.Suit.ToString(),
                rank = request.Rank.ToString(),
                nextPlayerId,
                gameStatus = currentGame.Status.ToString()
            };

            await _mediator.Publish(new GameEventNotification(
                    request.GameId,
                    "card_played",
                    JsonSerializer.Serialize(payload))
                ,cancellationToken);

            return Unit.Value;
        }
    }
}
