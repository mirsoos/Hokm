using MediatR;

namespace Hokm.Application.Features.DeductCoins.Commands
{
    public record DeductCoinsCommand(List<Guid> userIds, int Amount) : IRequest<Unit>;
}
