using Hokm.Application.DTOs;
using MediatR;

namespace Hokm.Application.Features.GetRandomBot.Queries
{
    public record GetRandomBotQuery(int Count, List<Guid> ExcludeUserIds) : IRequest<List<PlayerDto>>;

}
