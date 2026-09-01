using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.StartTrumpSelectionTimer
{
    public record StartTrumpSelectionTimerCommand(Guid GameId, Guid HakemId) : IRequest<Unit>;
}
