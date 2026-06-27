using Hokm.Application.DTOs;
using Hokm.Domain.Entities;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.GameStarted.Queries
{
    public class GetGameStateQueryHandler : IRequestHandler<GetGameStateQuery, GameStateDto>
    {
        private readonly IGameRepository _gameRepository;

        public GetGameStateQueryHandler(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }

        public async Task<GameStateDto> Handle(GetGameStateQuery request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            return new GameStateDto
            {
                GameId = currentGame.Id,
                Status = currentGame.Status,
                CurrentRound = currentGame.CurrentRoundIndex ?? 0
            };
        }
    }
}
