using Hokm.Domain.Enums;

namespace Hokm.Domain.ValueObjects
{
    public class Card : IEquatable<Card>
    {
        public Suit Suit { get; }
        public Rank Rank { get; }

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public bool Beats(Card other, Suit trumpSuit, Suit ledSuit)
        {
            bool thisIsTrump = Suit == trumpSuit;
            bool otherIsTrump = other.Suit == trumpSuit;

            if (thisIsTrump && !otherIsTrump) return true;
            if (!thisIsTrump && otherIsTrump) return false;

            if (Suit == other.Suit)
                return (int)Rank > (int)other.Rank;

            if (Suit == ledSuit) return true;
            if (other.Suit == ledSuit) return false;

            return false;
        }

        public override bool Equals(object obj) => Equals(obj as Card);
        public bool Equals(Card other) => other != null && Suit == other.Suit && Rank == other.Rank;
        public override int GetHashCode() => HashCode.Combine(Suit, Rank);
        public override string ToString() => $"{Rank} of {Suit}";
    }
}
