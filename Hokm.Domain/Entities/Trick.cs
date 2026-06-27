using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace Hokm.Domain.Entities
{
    public class Trick : BaseEntity
    {
        public Dictionary<Guid, Card> PlayedCards { get; private set; }
        public Suit? TrumpSuit { get; private set; }
        public Suit? LedSuit { get; private set; }
        public Guid? LeadPlayerId { get; private set; }
        public Guid? WinnerPlayerId { get; private set; }
        public List<Guid> PlayerOrder { get; private set; }
        public bool IsComplete => PlayedCards.Count == 4;

        public Trick(Guid leadPlayer, Suit? trumpSuit, List<Guid> playerOrder)
        {
            LeadPlayerId = leadPlayer;
            TrumpSuit = trumpSuit;
            PlayerOrder = playerOrder ?? throw new ArgumentNullException(nameof(playerOrder));
            PlayedCards = new Dictionary<Guid, Card>();
            LedSuit = null;
            WinnerPlayerId = null;
        }

        public void PlayCard(Guid playerId, Card card)
        {
            if (IsComplete)
                throw new InvalidOperationException("Trick is already complete.");
            var expectedPlayer = PlayerOrder[PlayedCards.Count];
            if (playerId != expectedPlayer)
                throw new InvalidOperationException($"It's not your turn. Expected player: {expectedPlayer}.");

            if (PlayedCards.Count == 0)
                LedSuit = card.Suit;

            PlayedCards.Add(playerId, card);

            if (IsComplete)
                DetermineWinner();
        }

        private void DetermineWinner()
        {
            if (PlayedCards.Count != 4 || LedSuit == null)
                throw new InvalidOperationException("Cannot determine winner yet.");
            Guid winnerId = default;
            Card? bestCard = null;
            foreach (var entry in PlayedCards)
            {
                if (bestCard == null || entry.Value.Beats(bestCard, TrumpSuit.Value, LedSuit.Value))
                {
                    bestCard = entry.Value;
                    winnerId = entry.Key;
                }
            }
            WinnerPlayerId = winnerId;
        }

        [JsonConstructor]
        public Trick() { }
    }
}
