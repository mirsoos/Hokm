namespace Hokm.Application.DTOs.GameSnapshot
{
    public class GameSnapshotDto
    {
        public Guid GameId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string TrumpSuit { get; set; } = string.Empty;
        public Guid? CurrentTurnPlayerId { get; set; }
        public List<PlayerSnapshotDto> Players { get; set; } = new();
        public List<CardDto> YourHand { get; set; } = new();
        public List<PlayedCardDto> CurrentTrick { get; set; } = new();
        public int RedTeamScore { get; set; } // تعداد راندهای برده شده قرمز (امتیاز کلی)
        public int BlueTeamScore { get; set; } // تعداد راندهای برده شده آبی (امتیاز کلی)
        public int RedTeamTricks { get; set; } // دست‌های برده شده قرمز در راند جاری (جدید)
        public int BlueTeamTricks { get; set; } // دست‌های برده شده آبی در راند جاری (جدید)
    }
}