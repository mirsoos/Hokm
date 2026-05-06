using MediatR;

namespace Hokm.Application.Features.DealCards.Command
{
    public class DealCardsCommand : IRequest<Unit>
    {
        public Guid GameId { get; set; }
        public Guid DealerId { get; set; }
    }
}
