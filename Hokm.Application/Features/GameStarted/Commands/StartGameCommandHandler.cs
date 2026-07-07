// Application/Features/GameStarted/Commands/StartGameCommandHandler.cs

using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.GameStarted.Commands
{
    public class StartGameCommandHandler : IRequestHandler<StartGameCommand, Guid>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IUserRepository _userRepository;

        public StartGameCommandHandler(IGameRepository gameRepository, IUserRepository userRepository)
        {
            _gameRepository = gameRepository;
            _userRepository = userRepository;
        }

        public async Task<Guid> Handle(StartGameCommand request, CancellationToken cancellationToken)
        {
            var player1 = new Player(request.Player1.PlayerId, request.Player1.Name, PlayerSide.South);
            var player2 = new Player(request.Player2.PlayerId, request.Player2.Name, PlayerSide.West);
            var player3 = new Player(request.Player3.PlayerId, request.Player3.Name, PlayerSide.North);
            var player4 = new Player(request.Player4.PlayerId, request.Player4.Name, PlayerSide.East);

            var newGame = new Domain.Entities.Game(player1, player2, player3, player4, request.TableKind);
            await _gameRepository.SaveAsync(newGame, cancellationToken);

            return newGame.Id;
        }
    }
}