using Hokm.Domain.Enums;
using System.Text.Json.Serialization;

namespace Hokm.Domain.Entities
{
    public class Team : BaseEntity
    {
        public List<Guid> PlayerIds { get; private set; }
        public int TotalScore { get; private set; } = 0;
        public TeamSide TeamSide { get; set; }

        public Team(Guid player1Id, Guid player2Id, TeamSide teamSide) : base()
        {
            PlayerIds = new List<Guid> { player1Id, player2Id };
            TeamSide = teamSide;
        }

        public void AddScore(int points)
        {
            TotalScore += points;
            IncrementVersion();
        }

        public void SubtractScore(int points)
        {
            TotalScore -= points;
            IncrementVersion();
        }

        [JsonConstructor]
        public Team() { }
    }
}