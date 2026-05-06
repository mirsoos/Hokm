using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.PlayCard.Commands
{
    public class PlayCardCommand : IRequest<Unit>
    {
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
        public Suit Suit { get; set; }
        public Rank Rank { get; set; }
    }
}
