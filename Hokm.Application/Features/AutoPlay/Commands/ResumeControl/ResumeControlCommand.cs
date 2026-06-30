using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.ResumeControl
{
    public record ResumeControlCommand(Guid GameId, Guid PlayerId) : IRequest<Unit>;

}
