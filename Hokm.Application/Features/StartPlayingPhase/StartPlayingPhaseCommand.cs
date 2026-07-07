using MediatR;

namespace Hokm.Application.Features.StartPlayingPhase
{
    public record StartPlayingPhaseCommand(Guid GameId) : IRequest<Unit>;

}
