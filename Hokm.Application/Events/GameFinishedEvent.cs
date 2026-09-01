using Hokm.Application.DTOs;

namespace Hokm.Application.Events
{
    public class GameFinishedEvent
    {
        public Guid? WinnerTeamId { get; set; }
        public List<TeamScoreDto> FinalScores { get; set; }
        public long Reward { get; set; }
    }
}
