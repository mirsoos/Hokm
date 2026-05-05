using Hokm.Domain.Enums;

namespace Application.DTOs
{
    public class GameStateDto
    {
        public Guid GameId { get; set; }
        public GameStatus Status { get; set; }
        public int CurrentRound { get; set; }
    }
}
