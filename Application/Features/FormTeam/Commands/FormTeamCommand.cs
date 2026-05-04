using Application.DTOs;
using MediatR;

namespace Application.Features.FormTeam.Commands
{
    public class FormTeamCommand : IRequest<FormedGamedDto>
    {
        public Guid GameId { get; set; }
    }
}
