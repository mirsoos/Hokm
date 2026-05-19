using Hokm.Domain.Enums;

namespace Hokm.Domain.Entities
{
    public class Player : BaseEntity
    {
        public string Name { get; private set; }
        public Guid UserId { get; set; }
        public Guid TeamId { get; private set; }
        public PlayerSide PlayerSide { get; private set; }

        public Player(Guid id, string name , PlayerSide playerSide) : base(id)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Player name cannot be empty.", nameof(name));

            Name = name;
            PlayerSide = playerSide;
        }
        public void AssignToTeam(Guid teamId) => TeamId = teamId;
    }

}
