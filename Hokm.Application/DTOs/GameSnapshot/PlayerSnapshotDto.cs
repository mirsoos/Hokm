using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hokm.Application.DTOs.GameSnapshot
{
    public class PlayerSnapshotDto
    {
        public Guid PlayerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public bool IsHakem { get; set; }
        public int Avatar { get; set; } = 1;
        public int CardCount { get; set; }
        public bool IsAutoPlay { get; set; }
        public string CardSkin { get; set; } = "default";
        public string BoardTheme { get; set; } = "default";
    }
}
