using MediatR;

namespace Hokm.Application.Features.ReadyToPlay.Commands
{
    public sealed record ReadyToPlayCommand(Guid GameId, Guid PlayerId) : IRequest<Unit>;

}
