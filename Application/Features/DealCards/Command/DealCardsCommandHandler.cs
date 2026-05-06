using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.DealCards.Command
{
    public class DealCardsCommandHandler : IRequestHandler<DealCardsCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        public DealCardsCommandHandler(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }
        public async Task<Unit> Handle(DealCardsCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null)
                throw new ArgumentNullException("Game not found with Id",nameof(request.GameId));
            game.StartRoundAndDeal(request.DealerId);
            await _gameRepository.UpdateAsync(game, cancellationToken);
            return Unit.Value;
        }
    }
}
