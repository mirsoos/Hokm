using Hokm.Domain.Entities;

namespace Hokm.Domain.Interfaces
{
    public interface IGameRepository
    {
        Task<Game> GetByIdAsync(Guid gameId);
        Task<Game> SaveAsync(Game game);
        Task UpdateAsync(Game game);
        Task<bool> ExistsAsync(Guid gameId);
    }
}
