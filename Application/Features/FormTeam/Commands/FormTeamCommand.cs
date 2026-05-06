using Hokm.Application.DTOs;
using MediatR;

namespace Hokm.Application.Features.FormTeam.Commands
{
    public class FormTeamCommand : IRequest<FormedGamedDto>
    {
        public Guid GameId { get; set; }
    }
}
