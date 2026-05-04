using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;

namespace Hokm.Domain.Entities
{
    public class Round
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public int RoundNumber { get; private set; }
        public Guid DealerId { get; private set; }
        public Suit TrumpSuit { get; private set; } = Suit.Deciding;
        public List<Trick> Tricks { get; private set; }
        public bool IsFinished { get; private set; }
        public Dictionary<Guid, List<Card>> PlayerHands { get; private set; }

        public Round(int roundNumber, Guid dealerId)
        {
            RoundNumber = roundNumber;
            DealerId = dealerId;
            Tricks = new List<Trick>();
            PlayerHands = new Dictionary<Guid, List<Card>>();
            IsFinished = false;
        }

        public void EndRound()
        {
            IsFinished = true;
        }
        public void SetTrump(Suit trumpSuit)
        {
            if (TrumpSuit != Suit.Deciding)
                throw new InvalidOperationException("Trump has already been set.");
            if (trumpSuit == Suit.Deciding)
                throw new ArgumentException("Trump suit cannot be Deciding.");
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
            // در حکم، تیمی که ۷ تریک یا بیشتر برده باشد برنده راند است
            foreach (var kv in teamTrickCount)
            {
                if (kv.Value >= 7)
                    return kv.Key;
            }
            return null; // نباید رخ دهد ولی در صورت خطا
        }

    }
    
}
