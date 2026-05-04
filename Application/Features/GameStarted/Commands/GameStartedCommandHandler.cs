using Hokm.Domain.Entities;
using Hokm.Domain.Interfaces;
using MediatR;

namespace Application.Features.GameStarted.Commands
{
    public class GameStartedCommandHandler : IRequestHandler<GameStartedCommand, Guid>
    {
        private readonly IGameRepository _gameRepository;
        public GameStartedCommandHandler(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }
        public async Task<Guid> Handle(GameStartedCommand request, CancellationToken cancellationToken)
        {
            var player1 = new Player(request.Player1.PlayerId,request.Player1.Name,request.Player1.Side);
            var player2 = new Player(request.Player2.PlayerId,request.Player2.Name,request.Player2.Side);
            var player3 = new Player(request.Player3.PlayerId,request.Player3.Name,request.Player3.Side);
            var player4 = new Player(request.Player4.PlayerId,request.Player4.Name,request.Player4.Side);

            var newGame = new Game(player1, player2, player3, player4);
            await _gameRepository.SaveAsync(newGame);
            return newGame.Id;
        }
    }
}
