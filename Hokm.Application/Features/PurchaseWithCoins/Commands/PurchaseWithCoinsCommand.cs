using ErrorOr;
using Hokm.Application.DTOs.Payment;
using MediatR;

namespace Hokm.Application.Features.PurchaseWithCoins.Commands
{
    public record PurchaseWithCoinsCommand(
        Guid UserId,
        Guid ProductId
    ) : IRequest<ErrorOr<PurchaseWithCoinsResultDto>>;
}
