using Application.DTOs;
using MediatR;

namespace Application.Features.GameStarted.Commands
{
    public class GameStartedCommand : IRequest<Guid>
    {
        public PlayerDto Player1 { get; set; }
        public PlayerDto Player2 { get; set; }
        public PlayerDto Player3 { get; set; }
        public PlayerDto Player4 { get; set; }
    }
}
