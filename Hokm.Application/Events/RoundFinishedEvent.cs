using Hokm.Application.DTOs;

namespace Hokm.Application.Events
{
    public class RoundFinishedEvent
    {
        public List<TeamScoreDto> Scores { get; set; }
    }
}
