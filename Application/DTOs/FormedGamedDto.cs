namespace Hokm.Application.DTOs
{
    public class FormedGamedDto
    {
        public Guid GameId { get; set; }
        public PlayerDto NorthPlayer { get; set; }
        public PlayerDto EastPlayer { get; set; }
        public PlayerDto SouthPlayer { get; set; }
        public PlayerDto WestPlayer { get; set; }
    }
}
