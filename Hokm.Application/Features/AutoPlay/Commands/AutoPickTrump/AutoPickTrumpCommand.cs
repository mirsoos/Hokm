using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.AutoPickTrump
{
    public record AutoPickTrumpCommand(Guid GameId, Guid HakemId) : IRequest<Unit>;

}
