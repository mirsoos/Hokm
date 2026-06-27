// Deck.cs
using Hokm.Domain.Enums;
using System.Text.Json.Serialization;

namespace Hokm.Domain.ValueObjects
{
    public class Deck
    {
        [JsonInclude]
        public List<Card> Cards { get; private set; }

        public Deck()
        {
            Cards = new List<Card>();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    Cards.Add(new Card(suit, rank));
                }
            }
        }

        [JsonConstructor]
        public Deck(List<Card> cards)
        {
            Cards = cards ?? new List<Card>();
        }

        public void Shuffle()
        {
            int n = Cards.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Shared.Next(n + 1);
                (Cards[k], Cards[n]) = (Cards[n], Cards[k]);
            }
        }

        public List<Card> Deal(int count)
        {
            if (count > Cards.Count)
                throw new InvalidOperationException("Not enough cards in deck.");

            var dealt = Cards.Take(count).ToList();

            Cards.RemoveRange(0, count);

            return dealt;
        }
    }
}