using Hokm.Application.Realtime.Contracts;
using Hokm.Domain.ValueObjects;

namespace Hokm.Application.Realtime.Mappers
{
    public static class RealtimeMapper
    {
        public static CardDto ToDto(Card card)
        {
            return new CardDto
            {
                Suit = card.Suit.ToString(),
                Rank = card.Rank.ToString()
            };
        }
    }
}