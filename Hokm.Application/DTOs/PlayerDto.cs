using Hokm.Domain.Enums;

namespace Hokm.Application.DTOs
{
    public class PlayerDto
    {
        public Guid PlayerId { get; set; }
        public PlayerSide Side { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Avatar { get; set; } = 1;
    }
}
