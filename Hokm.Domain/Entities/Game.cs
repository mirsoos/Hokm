using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace Hokm.Domain.Entities
{
    public class Game : BaseEntity
    {
        [JsonInclude]
        public List<Player> Players { get; private set; }
        [JsonInclude]
        public List<Team> Teams { get; private set; }
        [JsonInclude]
        public List<Round> Rounds { get; private set; }
        [JsonInclude]
        public GameStatus Status { get; private set; }
        [JsonInclude]
        public int? CurrentRoundIndex { get; private set; }
        [JsonInclude]
        public Guid? LastTrickWinnerPlayerId { get; private set; }
        [JsonInclude]
        public List<Guid> WinnerPlayers { get; private set; }

        public Game(Player player1, Player player2, Player player3, Player player4)
        {
            Players = new List<Player> { player1, player2, player3, player4 };
            Teams = new List<Team>();
            Rounds = new List<Round>();
            Status = GameStatus.WaitingForTeams;
            CurrentRoundIndex = null;
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

        public Dictionary<Guid, List<Card>> StartRoundAndDeal(Guid dealerId)
        {
            if (Status != GameStatus.TeamsReady && Status != GameStatus.RoundFinished)
                throw new InvalidOperationException("Game is not ready for a new round.");

            var roundNumber = Rounds.Count + 1;
            var round = new Round(roundNumber, dealerId);
            Rounds.Add(round);
            CurrentRoundIndex = Rounds.Count - 1;
            foreach (var player in Players)
            {
                round.PlayerHands[player.Id] = new List<Card>();
            }
            Status = GameStatus.DealingFirstFiveCards;
            var order = GetTurnOrderForDeal(dealerId);
            var dealtCards = round.DealCards(order, 5);
            Status = GameStatus.WaitingForTrumpSelection;
            return dealtCards;
        }

        private List<Guid> GetTurnOrderForDeal(Guid dealerId)
        {
            var dealer = Players.First(x => x.Id == dealerId);
            var firstSide = GetRightSideOf(dealer.PlayerSide);
            var startIndex = CounterClockwiseOrder.IndexOf(firstSide);
            var orderSides = Enumerable.Range(0, 4).Select(i => CounterClockwiseOrder[(startIndex + i) % 4]).ToList();
            return orderSides.Select(side => Players.First(x => x.PlayerSide == side).Id).ToList();
        }
        public Dictionary<Guid, List<Card>> StartNextRound()
        {
            if (Status != GameStatus.RoundFinished)
                throw new InvalidOperationException("Game is not in a state to start the next round.");

            if (Rounds.Any())
            {
                var lastRound = Rounds.Last();

                var winningTeamId = lastRound.GetWinningTeamId(Teams);

                var lastDealer = Players.First(p => p.Id == lastRound.DealerId);
                var lastHakemSide = GetRightSideOf(lastDealer.PlayerSide);
                var lastHakem = Players.First(p => p.PlayerSide == lastHakemSide);
                var lastHakemTeamId = Teams.First(t => t.PlayerIds.Contains(lastHakem.Id)).Id;

                Guid newDealerId;

                if (winningTeamId == lastHakemTeamId)
                {
                    newDealerId = lastRound.DealerId;
                }
                else
                {
                    var newDealerSide = GetRightSideOf(lastDealer.PlayerSide);
                    newDealerId = Players.First(p => p.PlayerSide == newDealerSide).Id;
                }

                return StartRoundAndDeal(newDealerId);
            }
            else
            {
                throw new InvalidOperationException("No previous round to base the next dealer on.");
            }
        }

        public Dictionary<Guid, List<Card>> SetTrumpForCurrentRound(Suit trumpSuit, Guid hakemId)
        {
            if (Status != GameStatus.WaitingForTrumpSelection || CurrentRoundIndex == null)
                throw new InvalidOperationException("Game is not waiting for trump.");

            var round = Rounds[CurrentRoundIndex.Value];

            var dealer = Players.First(x => x.Id == round.DealerId);
            var hakemSide = GetRightSideOf(dealer.PlayerSide);
            var expectedHakemId = Players.First(x => x.PlayerSide == hakemSide).Id;

            if (expectedHakemId != hakemId)
                throw new InvalidOperationException("Only the Hakem can select trump.");

            round.SetTrump(trumpSuit);
            Status = GameStatus.DealingRemainingCards;
            var order = GetTurnOrderForDeal(round.DealerId);
            var secondFour = round.DealCards(order, 4);
            var lastFour = round.DealCards(order, 4);
            var allNewCards = new Dictionary<Guid, List<Card>>();
            foreach (var playerId in secondFour.Keys)
            {
                allNewCards[playerId] = secondFour[playerId].Concat(lastFour[playerId]).ToList();
            }
            Status = GameStatus.Playing;

            var leadPlayerId = hakemId;
            var firstTrick = new Trick(leadPlayerId, trumpSuit, GetTurnOrderForTrick(leadPlayerId));
            round.Tricks.Add(firstTrick);
            return allNewCards;
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
            var startIdx = CounterClockwiseOrder.IndexOf(leadSide);
            var orderSides = Enumerable.Range(0, 4)
                .Select(i => CounterClockwiseOrder[(startIdx + i) % 4])
                .ToList();
            return orderSides.Select(side => Players.First(p => p.PlayerSide == side).Id).ToList();
        }

        private static readonly List<PlayerSide> CounterClockwiseOrder = new()
            { PlayerSide.North, PlayerSide.West, PlayerSide.South, PlayerSide.East };

        private Trick? CurrentTrick => CurrentRoundIndex.HasValue ? Rounds[CurrentRoundIndex.Value].Tricks.LastOrDefault(t => !t.IsComplete) : null;

        public void PlayCard(Guid playerId, Card card)
        {
            LastTrickWinnerPlayerId = null;
            if (Status != GameStatus.Playing)
                throw new InvalidOperationException("Game is not in playing state.");
            if (!CurrentRoundIndex.HasValue)
                throw new InvalidOperationException("No active round.");

            var round = Rounds[CurrentRoundIndex.Value];
            var trick = round.Tricks.LastOrDefault(t => !t.IsComplete);

            if (trick == null)
                throw new InvalidOperationException("No active trick. Start a new trick first.");

            if (!round.PlayerHands.ContainsKey(playerId))
                throw new InvalidOperationException("Player not found in round.");

            var playerHand = round.PlayerHands[playerId];
            if (!playerHand.Contains(card))
                throw new InvalidOperationException("Player does not own this card.");

            if (trick.LedSuit.HasValue && card.Suit != trick.LedSuit.Value)
            {
                var hasLedSuit = playerHand.Any(x => x.Suit == trick.LedSuit.Value);
                if (hasLedSuit)
                    throw new InvalidOperationException("Player must follow led suit.");
            }

            trick.PlayCard(playerId, card);
            playerHand.Remove(card);

            if (trick.IsComplete)
            {
                LastTrickWinnerPlayerId = trick.WinnerPlayerId;

                int team1Tricks = 0;
                int team2Tricks = 0;

                if (Teams.Count >= 2)
                {
                    var team1 = Teams[0];
                    var team2 = Teams[1];

                    team1Tricks = round.Tricks.Count(t => t.IsComplete && t.WinnerPlayerId.HasValue && team1.PlayerIds.Contains(t.WinnerPlayerId.Value));
                    team2Tricks = round.Tricks.Count(t => t.IsComplete && t.WinnerPlayerId.HasValue && team2.PlayerIds.Contains(t.WinnerPlayerId.Value));
                }

                if (team1Tricks >= 7 || team2Tricks >= 7)
                {
                    round.EndRound();

                    var winningTeamId = team1Tricks >= 7 ? Teams[0].Id : Teams[1].Id;
                    var team = Teams.First(t => t.Id == winningTeamId);
                    team.AddScore(1);

                    if (Teams.Any(t => t.TotalScore >= 7))
                    {
                        EndGame();
                    }
                    else
                    {
                        Status = GameStatus.RoundFinished;
                    }
                }
                else
                {
                    var nextLead = trick.WinnerPlayerId!.Value;
                    var newTrick = new Trick(nextLead, round.TrumpSuit, GetTurnOrderForTrick(nextLead));
                    round.Tricks.Add(newTrick);
                }
            }
        }

        public bool IsCardPlayable(Guid playerId, Card card)
        {
            if (Status != GameStatus.Playing || !CurrentRoundIndex.HasValue)
                return false;

            if (GetCurrentTurnPlayerId() != playerId)
                return false;

            var round = Rounds[CurrentRoundIndex.Value];
            var trick = round.Tricks.LastOrDefault(t => !t.IsComplete);
            if (trick == null)
                return false;

            if (!round.PlayerHands.ContainsKey(playerId))
                return false;

            var playerHand = round.PlayerHands[playerId];

            if (!playerHand.Contains(card))
                return false;

            if (trick.LedSuit.HasValue && card.Suit != trick.LedSuit.Value)
            {
                var hasLedSuit = playerHand.Any(x => x.Suit == trick.LedSuit.Value);
                if (hasLedSuit)
                    return false;
            }

            return true;
        }

        public Guid? GetCurrentTurnPlayerId()
        {
            if (CurrentTrick == null) return null;

            if (CurrentTrick.IsComplete) return null;

            return CurrentTrick.PlayerOrder[CurrentTrick.PlayedCards.Count];
        }

        public PlayerSide GetRightSideOf(PlayerSide side) => side switch
        {
            PlayerSide.North => PlayerSide.West,
            PlayerSide.West => PlayerSide.South,
            PlayerSide.South => PlayerSide.East,
            PlayerSide.East => PlayerSide.North,
            _ => throw new ArgumentOutOfRangeException()
        };
        [JsonConstructor]
        public Game() { }
    }    
}
