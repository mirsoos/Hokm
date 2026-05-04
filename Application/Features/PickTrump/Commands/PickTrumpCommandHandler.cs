using Hokm.Domain.Enums;
using Hokm.Domain.Interfaces;
using MediatR;

namespace Application.Features.PickTrump.Commands
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
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId);
            if(currentGame == null)
                throw new ArgumentNullException("Game not found with Id",nameof(request.GameId));

            currentGame.SetTrumpForCurrentRound(request.TrumpSuit);
            await _gameRepository.UpdateAsync(currentGame);
            return Unit.Value;
        }
    }
}
