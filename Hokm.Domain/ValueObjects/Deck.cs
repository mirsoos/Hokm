using Hokm.Domain.Enums;

namespace Hokm.Domain.ValueObjects
{
    public class Deck
    {
        private List<Card> _cards;

        public Deck()
        {
            _cards = new List<Card>();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                if (suit == Suit.Deciding) continue;
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    _cards.Add(new Card(suit, rank));
                }
            }
        }

        public void Shuffle()
        {
            var rng = new Random();
            int n = _cards.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (_cards[k], _cards[n]) = (_cards[n], _cards[k]);
            }
        }

        public List<Card> Deal(int count)
        {
            if (count > _cards.Count)
                throw new InvalidOperationException("Not enough cards in deck.");
            var dealt = _cards.Take(count).ToList();
            _cards = _cards.Skip(count).ToList();
            return dealt;
        }
    }
}
