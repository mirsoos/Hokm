using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hokm.Application.DTOs.GameSnapshot
{
    public class PlayedCardDto
    {
        public Guid PlayerId { get; set; }
        public string Rank { get; set; } = string.Empty;
        public string Suit { get; set; } = string.Empty;
    }
}
