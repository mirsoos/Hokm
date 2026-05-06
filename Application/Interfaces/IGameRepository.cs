using Hokm.Domain.Entities;

namespace Hokm.Application.Interfaces
{
    public interface IGameRepository
    {
        Task<Game> GetByIdAsync(Guid gameId , CancellationToken cancellationToken);
        Task<Game> SaveAsync(Game game , CancellationToken cancellationToken);
        Task UpdateAsync(Game game , CancellationToken cancellationToken);
        Task<bool> ExistsAsync(Guid gameId , CancellationToken cancellationToken);
    }
}
