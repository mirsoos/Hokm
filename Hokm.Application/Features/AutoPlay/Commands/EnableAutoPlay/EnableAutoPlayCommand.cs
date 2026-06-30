using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.EnableAutoPlay
{
    public record EnableAutoPlayCommand(Guid GameId, Guid PlayerId) : IRequest<Unit>;

}
