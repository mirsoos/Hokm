using ErrorOr;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.Game.Queries.GetGameHistory
{
    public class GetGameHistoryQueryHandler : IRequestHandler<GetGameHistoryQuery, ErrorOr<List<GameHistoryItem>?>>
    {
        private readonly IGameRepository _gameRepository;
        public GetGameHistoryQueryHandler(IGameRepository gameRepository)
        {
            _gameRepository = gameRepository;
        }

        public async Task<ErrorOr<List<GameHistoryItem>?>> Handle(GetGameHistoryQuery request, CancellationToken cancellationToken)
        {
            return await _gameRepository.GetHistoryByUserIdAsync(request.UserId, request.Take, cancellationToken);
        }
    }
}
