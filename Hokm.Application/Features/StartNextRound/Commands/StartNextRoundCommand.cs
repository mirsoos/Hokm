using MediatR;

namespace Hokm.Application.Features.StartNextRound.Commands
{
    public class StartNextRoundCommand : IRequest<Unit> 
    {
        public Guid GameId { get; set; }
    }
}
