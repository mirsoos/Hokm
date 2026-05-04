using Hokm.Domain.Enums;

namespace Hokm.Domain.Entities
{
    public class Player
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Name { get; private set; }
        public ConnectionStatus Status { get; private set; }
        public Guid TeamId { get; private set; }
        public PlayerSide PlayerSide { get; private set; }

        public Player(Guid id, string name , PlayerSide playerSide)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Player name cannot be empty.", nameof(name));

            Id = id;
            Name = name;
            PlayerSide = playerSide;
            Status = ConnectionStatus.Online;
        }

        public void SetOnline() => Status = ConnectionStatus.Online;
        public void SetOffline() => Status = ConnectionStatus.Offline;
        public void AssignToTeam(Guid teamId) => TeamId = teamId;
    }

}
