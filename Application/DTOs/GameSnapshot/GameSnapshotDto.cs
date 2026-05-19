
namespace Hokm.Application.DTOs.GameSnapshot
{
    public class GameSnapshotDto
    {
        public Guid GameId { get; set; }

        public string Status { get; set; }

        public string TrumpSuit { get; set; }

        public Guid? CurrentTurnPlayerId { get; set; }

        public List<PlayerSnapshotDto> Players { get; set; }

        public List<CardDto> YourHand { get; set; }

        public List<PlayedCardDto> CurrentTrick { get; set; }

        public int RedTeamScore { get; set; }

        public int BlueTeamScore { get; set; }
    }
}
