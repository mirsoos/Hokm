using Hokm.Domain.Enums;
using System.Text.Json.Serialization;

namespace Hokm.Domain.Entities
{
    public class Player : BaseEntity
    {
        public string Name { get; private set; }
        public Guid UserId { get; set; }
        public Guid TeamId { get; private set; }
        public PlayerSide PlayerSide { get; private set; }
        public int AvatarRef { get; private set; } = 1;
        public bool IsAutoPlay { get; private set; } = false;

        public string CardSkin { get; private set; } = "default";
        public string BoardTheme { get; private set; } = "default";

        public Player(Guid id, string name, PlayerSide playerSide) : base(id)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Player name cannot be empty.", nameof(name));

            Name = name;
            PlayerSide = playerSide;
        }

        public void SetAvatarRef(int avatarRef)
        {
            if (avatarRef > 0)
            {
                AvatarRef = avatarRef;
                IncrementVersion();
            }
        }

        public void SetCardSkin(string cardSkin)
        {
            if (!string.IsNullOrWhiteSpace(cardSkin))
            {
                CardSkin = cardSkin;
                IncrementVersion();
            }
        }

        public void SetBoardTheme(string boardTheme)
        {
            if (!string.IsNullOrWhiteSpace(boardTheme))
            {
                BoardTheme = boardTheme;
                IncrementVersion();
            }
        }

        public void AssignToTeam(Guid teamId)
        {
            TeamId = teamId;
            IncrementVersion();
        }

        public void EnableAutoPlay()
        {
            IsAutoPlay = true;
            IncrementVersion();
        }

        public void DisableAutoPlay()
        {
            IsAutoPlay = false;
            IncrementVersion();
        }

        [JsonConstructor]
        public Player() { }
    }
}