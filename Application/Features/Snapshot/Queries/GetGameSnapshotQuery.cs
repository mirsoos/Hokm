using Hokm.Application.DTOs.GameSnapshot;
using MediatR;

namespace Hokm.Application.Features.Snapshot.Queries
{
    public class GetGameSnapshotQuery : IRequest<GameSnapshotDto>
    {
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
    }
}
