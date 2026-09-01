using ErrorOr;
using Hokm.Application.DTOs.Payment;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.VerifyStorePurchase.Commands
{
    public record VerifyStorePurchaseCommand(
         Guid UserId,
         Guid ProductId,
         string PurchaseToken,
         GatewayType Gateway
     ) : IRequest<ErrorOr<VerifyStorePurchaseResultDto>>;
}
