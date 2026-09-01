using Hokm.Domain.Enums;
using System.Text.Json.Serialization;

namespace Hokm.Domain.ValueObjects
{
    public class Card : IEquatable<Card>
    {
        [JsonInclude]
        public Suit Suit { get; private set; }

        [JsonInclude]
        public Rank Rank { get; private set; }

        [JsonConstructor]
        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public bool Beats(Card other, Suit trumpSuit, Suit ledSuit)
        {
            if (other == null) return true;

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

        public override bool Equals(object? obj) => Equals(obj as Card);

        public bool Equals(Card? other) => other != null && Suit == other.Suit && Rank == other.Rank;

        public override int GetHashCode() => HashCode.Combine(Suit, Rank);

        public override string ToString() => $"{Rank} of {Suit}";

        public static bool operator ==(Card? left, Card? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(Card? left, Card? right) => !(left == right);
    }
}