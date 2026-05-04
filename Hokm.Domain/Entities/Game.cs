using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;

namespace Hokm.Domain.Entities
{
    public class Game
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public List<Player> Players { get; private set; }
        public List<Team> Teams { get; private set; }
        public List<Round> Rounds { get; private set; }
        public GameStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public int? CurrentRoundIndex { get; private set; }
        public int? CurrentTrickIndex { get; private set; }


        public Game(Player player1, Player player2, Player player3, Player player4)
        {
            Players = new List<Player> { player1, player2, player3, player4 };
            Teams = new List<Team>();
            Rounds = new List<Round>();
            Status = GameStatus.WaitingForTeams;
            CreatedAt = DateTime.UtcNow;
            CurrentRoundIndex = null;
            CurrentTrickIndex = null;
        }

        public void FormTeams(Team team1, Team team2)
        {
            if (Teams.Any())
                throw new InvalidOperationException("Teams have already been formed.");
            if (team1.PlayerIds.Intersect(team2.PlayerIds).Any())
                throw new InvalidOperationException("A player cannot belong to both teams.");
            if (team1.PlayerIds.Count != 2 || team2.PlayerIds.Count != 2)
                throw new InvalidOperationException("Each team must have exactly 2 players.");

            Teams.Add(team1);
            Teams.Add(team2);

            foreach (var player in Players)
            {
                if (team1.PlayerIds.Contains(player.Id))
                    player.AssignToTeam(team1.Id);
                else if (team2.PlayerIds.Contains(player.Id))
                    player.AssignToTeam(team2.Id);
            }

            Status = GameStatus.TeamsReady;
        }

        public void StartRoundAndDeal(Guid dealerId)
        {
            if (Status != GameStatus.TeamsReady && Status != GameStatus.RoundFinished)
                throw new InvalidOperationException("Game is not ready for a new round.");

            var roundNumber = Rounds.Count + 1;
            var round = new Round(roundNumber, dealerId);
            Rounds.Add(round);
            CurrentRoundIndex = Rounds.Count - 1;

            var deck = new Deck();
            deck.Shuffle();
            foreach (var player in Players)
            {
                var hand = deck.Deal(13);
                round.PlayerHands[player.Id] = hand;
            }

            Status = GameStatus.WaitingForTrump;
        }
        public void StartNextRound()
        {
            if (Status != GameStatus.RoundFinished)
                throw new InvalidOperationException("Game is not in a state to start the next round.");
            if (CurrentRoundIndex == null && Rounds.Any())
            {
                var lastRound = Rounds.Last();
                var lastDealer = Players.First(p => p.Id == lastRound.DealerId);
                var newDealerSide = GetRightSideOf(lastDealer.PlayerSide);
                var newDealer = Players.First(p => p.PlayerSide == newDealerSide);

                StartRoundAndDeal(newDealer.Id);
            }
            else
            {
                throw new InvalidOperationException("No previous round to base the next dealer on.");
            }
        }

        public void SetTrumpForCurrentRound(Suit trumpSuit)
        {
            if (Status != GameStatus.WaitingForTrump || CurrentRoundIndex == null)
                throw new InvalidOperationException("Game is not waiting for trump.");
            var round = Rounds[CurrentRoundIndex.Value];
            round.SetTrump(trumpSuit);
            Status = GameStatus.Playing;

            var dealerId = round.DealerId;
            var dealerSide = Players.First(p => p.Id == dealerId).PlayerSide;
            var leadSide = GetRightSideOf(dealerSide);
            var leadPlayerId = Players.First(p => p.PlayerSide == leadSide).Id;

            var firstTrick = new Trick(leadPlayerId, trumpSuit, GetTurnOrderForTrick(leadPlayerId));
            round.Tricks.Add(firstTrick);
        }

        public void EndGame()
        {
            Status = GameStatus.Finished;
            if (CurrentRoundIndex != null)
                Rounds[CurrentRoundIndex.Value].EndRound();
            CurrentRoundIndex = null;
        }

        private List<Guid> GetTurnOrderForTrick(Guid leadPlayerId)
        {
            var leadSide = Players.First(p => p.Id == leadPlayerId).PlayerSide;
            var startIdx = ClockwiseOrder.IndexOf(leadSide);
            var orderSides = Enumerable.Range(0, 4)
                .Select(i => ClockwiseOrder[(startIdx + i) % 4])
                .ToList();
            return orderSides.Select(side => Players.First(p => p.PlayerSide == side).Id).ToList();
        }

        private static readonly List<PlayerSide> ClockwiseOrder = new()
            { PlayerSide.North, PlayerSide.East, PlayerSide.South, PlayerSide.West };

        private Trick? CurrentTrick =>
            CurrentRoundIndex.HasValue ? Rounds[CurrentRoundIndex.Value].Tricks.LastOrDefault(t => !t.IsComplete) : null;

        public void PlayCard(Guid playerId, Card card)
        {
            if (Status != GameStatus.Playing)
                throw new InvalidOperationException("Game is not in playing state.");
            if (!CurrentRoundIndex.HasValue)
                throw new InvalidOperationException("No active round.");

            var round = Rounds[CurrentRoundIndex.Value];
            var trick = round.Tricks.LastOrDefault(t => !t.IsComplete);

            if (trick == null)
                throw new InvalidOperationException("No active trick. Start a new trick first.");

            // اعتبارسنجی نوبت و منطق بازی کارت داخل Trick انجام می‌شود
            trick.PlayCard(playerId, card);

            // اگر تریک کامل شد
            if (trick.IsComplete)
            {
                // اگر راند کامل شد (۱۳ تریک)
                if (round.Tricks.Count == 13)  // تعداد کل تریک‌های یک دست حکم
                {
                    round.EndRound();
                    // محاسبه تیم برنده راند
                    var winningTeamId = round.GetWinningTeamId(Teams);
                    if (winningTeamId != null)
                    {
                        var team = Teams.First(t => t.Id == winningTeamId.Value);
                        team.AddScore(1); // هر راند ۱ امتیاز به تیم برنده
                    }

                    if (Teams.Any(t => t.TotalScore >= 7))
                    {
                        EndGame();
                    }
                    else
                    {
                        Status = GameStatus.RoundFinished;      // <-- اضافه شود
                        CurrentRoundIndex = null;               // نیاز به فراخوانی دستی برای شروع راند بعدی
                    }
                }
                else
                {
                    // شروع تریک بعدی: لیدکننده آن برندهٔ تریک فعلی است
                    var nextLead = trick.WinnerPlayerId!.Value;
                    var newTrick = new Trick(nextLead, round.TrumpSuit, GetTurnOrderForTrick(nextLead));
                    round.Tricks.Add(newTrick);
                }
            }
        }

        private PlayerSide GetRightSideOf(PlayerSide side) => side switch
        {
            PlayerSide.North => PlayerSide.East,
            PlayerSide.East => PlayerSide.South,
            PlayerSide.South => PlayerSide.West,
            PlayerSide.West => PlayerSide.North,
            _ => throw new ArgumentOutOfRangeException()
        };

    }    
}
