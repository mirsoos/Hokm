using Hokm.Domain.Enums;

namespace Hokm.Application.DTOs
{
    public class PlayerDto
    {
        public Guid PlayerId { get; set; }
        public PlayerSide Side { get; set; }
        public string Name { get; set; }
    }
}
