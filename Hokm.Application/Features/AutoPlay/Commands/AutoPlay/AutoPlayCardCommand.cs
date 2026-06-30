using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.AutoPlay
{
    public record AutoPlayCardCommand(Guid GameId, Guid PlayerId) : IRequest<Unit>;
}
