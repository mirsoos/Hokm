using Application.DTOs;
using Hokm.Domain.Entities;
using MediatR;

namespace Application.Features.GameStarted.Queries
{
    public class GetGameStateQuery : IRequest<GameStateDto>
    {
        public Guid GameId { get; set; }
    }
}
