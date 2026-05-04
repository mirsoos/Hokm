using Application.DTOs;
using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using Hokm.Domain.Interfaces;
using MediatR;

namespace Application.Features.FormTeam.Commands
{
    public class FormTeamCommandHandler : IRequestHandler<FormTeamCommand, FormedGamedDto>
    {
        private readonly IGameRepository _gameRepository;
        public FormTeamCommandHandler(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }
        public async Task<FormedGamedDto> Handle(FormTeamCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId);
            if (currentGame == null)
                throw new ArgumentNullException("Game not found with Id.",nameof(request.GameId));

            var players = currentGame.Players;
            var northPlayer = players.First(x => x.PlayerSide == PlayerSide.North);
            var eastPlayer = players.First(x => x.PlayerSide == PlayerSide.East);
            var southPlayer = players.First(x => x.PlayerSide == PlayerSide.South);
            var westPlayer = players.First(x => x.PlayerSide == PlayerSide.West);

            var redTeam = new Team(northPlayer.Id, southPlayer.Id, TeamSide.Red);
            var blueTeam = new Team(eastPlayer.Id, westPlayer.Id, TeamSide.Blue);
            currentGame.FormTeams(redTeam,blueTeam);
            await _gameRepository.UpdateAsync(currentGame);
            return new FormedGamedDto
            {
                GameId = currentGame.Id,
                NorthPlayer = MapToPlayerDto(northPlayer),
                EastPlayer = MapToPlayerDto(eastPlayer),
                SouthPlayer = MapToPlayerDto(southPlayer),
                WestPlayer = MapToPlayerDto(westPlayer)
            };
        }
        private static PlayerDto MapToPlayerDto(Player p) => new()
        {
            PlayerId = p.Id,
            Name = p.Name,
            Side = p.PlayerSide
        };
    }
}
