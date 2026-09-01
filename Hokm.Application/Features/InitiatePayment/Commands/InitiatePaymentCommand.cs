using ErrorOr;
using Hokm.Application.DTOs.Payment;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.InitiatePayment.Commands
{
    public record InitiatePaymentCommand(
        Guid UserId,
        Guid ProductId,
        GatewayType Gateway
    ) : IRequest<ErrorOr<InitiatePaymentResultDto>>;
}
