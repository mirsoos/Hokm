using Hokm.Application.DTOs;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.GameStarted.Commands
{
    public class StartGameCommand : IRequest<Guid>
    {
        public PlayerDto Player1 { get; set; }
        public PlayerDto Player2 { get; set; }
        public PlayerDto Player3 { get; set; }
        public PlayerDto Player4 { get; set; }
        public TableKind TableKind { get; set; }
    }
}
