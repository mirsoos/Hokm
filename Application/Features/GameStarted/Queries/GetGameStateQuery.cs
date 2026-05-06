using Hokm.Application.DTOs;
using MediatR;

namespace Hokm.Application.Features.GameStarted.Queries
{
    public class GetGameStateQuery : IRequest<GameStateDto>
    {
        public Guid GameId { get; set; }
    }
}
