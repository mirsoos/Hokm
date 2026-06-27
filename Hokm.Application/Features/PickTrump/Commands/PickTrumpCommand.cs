using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.PickTrump.Commands
{
    public class PickTrumpCommand : IRequest<Unit>
    {
        public Guid GameId { get; set; }
        public Guid DealerId { get; set; }
        public Suit TrumpSuit { get; set; }
    }
}
