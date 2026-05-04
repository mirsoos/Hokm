using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.StartNextRound.Commands
{
    public class StartNextRoundCommand : IRequest<Unit> 
    {
        public Guid GameId { get; set; }
    }
}
