using Hokm.Domain.Entities;

namespace Hokm.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<Guid> AddAsync(User user);
    }
}
