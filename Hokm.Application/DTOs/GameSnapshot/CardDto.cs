
namespace Hokm.Application.DTOs.GameSnapshot
{
    public class CardDto
    {
        public string Rank { get; set; } = string.Empty;
        public string Suit { get; set; } = string.Empty;
        public bool IsPlayable { get; set; }
    }
}
