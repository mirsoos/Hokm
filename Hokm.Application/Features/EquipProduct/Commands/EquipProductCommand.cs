using ErrorOr;
using Hokm.Application.DTOs;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.EquipProduct.Commands
{
    public record EquipProductCommand(
        Guid UserId,
        Guid ProductId,
        ProductType ProductType
    ) : IRequest<ErrorOr<EquipProductResultDto>>;
}
