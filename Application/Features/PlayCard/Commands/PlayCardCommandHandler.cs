using Hokm.Application.Interfaces;
using Hokm.Domain.ValueObjects;
using MediatR;

namespace Hokm.Application.Features.PlayCard.Commands
{
    public class PlayCardCommandHandler : IRequestHandler<PlayCardCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        public PlayCardCommandHandler(IGameRepository gameRepository    )
        {
            _gameRepository = gameRepository;
        }

        public async Task<Unit> Handle(PlayCardCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (currentGame == null)
                throw new ArgumentNullException("Game not found with Id",nameof(request.GameId));
            var card = new Card(request.Suit,request.Rank);
            currentGame.PlayCard(request.PlayerId,card);
            await _gameRepository.UpdateAsync(currentGame, cancellationToken);
            return Unit.Value;
        }
    }
}
