using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.PickTrump.Commands
{
    public class PickTrumpCommandHandler : IRequestHandler<PickTrumpCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        public PickTrumpCommandHandler(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }
        public async Task<Unit> Handle(PickTrumpCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if(currentGame == null)
                throw new ArgumentNullException("Game not found with Id",nameof(request.GameId));

            currentGame.SetTrumpForCurrentRound(request.TrumpSuit);
            await _gameRepository.UpdateAsync(currentGame, cancellationToken);
            return Unit.Value;
        }
    }
}
