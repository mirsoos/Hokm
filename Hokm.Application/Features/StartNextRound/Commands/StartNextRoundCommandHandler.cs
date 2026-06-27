using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.StartNextRound.Commands
{
    public class StartNextRoundCommandHandler : IRequestHandler<StartNextRoundCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        public StartNextRoundCommandHandler(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }
        public async Task<Unit> Handle(StartNextRoundCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null)
                throw new ArgumentNullException("Game not found with Id",nameof(request.GameId));

            game.StartNextRound();
            await _gameRepository.UpdateAsync(game, cancellationToken);
            return Unit.Value;
        }
    }
}
