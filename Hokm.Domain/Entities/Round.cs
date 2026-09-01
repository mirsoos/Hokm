using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace Hokm.Domain.Entities
{
    public class Round : BaseEntity
    {
        public int RoundNumber { get; private set; }
        public Guid DealerId { get; private set; }
        public Guid HakemId { get; private set; }
        public Deck Deck { get; private set; }
        public Suit? TrumpSuit { get; private set; }
        public List<Trick> Tricks { get; private set; }
        public bool IsFinished { get; private set; }
        public Dictionary<Guid, List<Card>> PlayerHands { get; private set; }

        public Round(int roundNumber, Guid dealerId, Guid hakemId)
        {
            RoundNumber = roundNumber;
            DealerId = dealerId;
            HakemId = hakemId;
            Tricks = new List<Trick>();
            PlayerHands = new Dictionary<Guid, List<Card>>();
            IsFinished = false;
            Deck = new Deck();
            Deck.Shuffle();
        }

        public void EndRound()
        {
            IsFinished = true;
        }

        public void SetTrump(Suit trumpSuit)
        {
            if (TrumpSuit != null)
                throw new InvalidOperationException("Trump has already been set.");
            TrumpSuit = trumpSuit;
        }

        public Guid? GetWinningTeamId(List<Team> teams)
        {
            if (!IsFinished) throw new InvalidOperationException("Round is not finished.");
            var teamTrickCount = teams.ToDictionary(t => t.Id, t => 0);
            foreach (var trick in Tricks)
            {
                if (trick.WinnerPlayerId == null) continue;
                var winnerTeam = teams.FirstOrDefault(t => t.PlayerIds.Contains(trick.WinnerPlayerId.Value));
                if (winnerTeam != null)
                    teamTrickCount[winnerTeam.Id]++;
            }
            foreach (var kv in teamTrickCount)
            {
                if (kv.Value >= 7)
                    return kv.Key;
            }
            return null;
        }

        public Dictionary<Guid, List<Card>> DealCards(List<Guid> playerOrder, int count)
        {
            var result = new Dictionary<Guid, List<Card>>();

            foreach (var playerId in playerOrder)
            {
                var cards = Deck.Deal(count);
                if (!PlayerHands.ContainsKey(playerId))
                {
                    PlayerHands[playerId] = new List<Card>();
                }
                PlayerHands[playerId].AddRange(cards);
                result[playerId] = cards;
            }
            return result;
        }

        [JsonConstructor]
        public Round() { }
    }
}