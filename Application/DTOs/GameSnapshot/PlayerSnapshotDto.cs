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

        public string Name { get; set; }

        public string Side { get; set; }
    }
}
