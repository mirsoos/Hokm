using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs
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
