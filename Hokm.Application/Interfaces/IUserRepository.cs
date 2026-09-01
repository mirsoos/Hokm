using Hokm.Domain.Entities;

namespace Hokm.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<Guid> AddAsync(User user,CancellationToken cancellationToken);
        Task<User?> GetByPhoneNumberAsync(string phoneNumber , CancellationToken cancellationToken);
        Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
        Task UpdateProfileAsync(Guid userId,string fullName ,int avatarRef, CancellationToken cancellationToken);
        Task<bool> ExistsUserNameAsync(string userName, CancellationToken cancellationToken);
        Task<bool> DeleteByIdAsync(Guid userId, CancellationToken cancellationToken);
        Task<bool> ExistByIdAsync(Guid userId, CancellationToken cancellationToken);
        Task UpdateAvatarAsync(Guid userId,int avatarRef , CancellationToken cancellationToken);
        Task<List<User>> GetFirstFourUsersAsync(CancellationToken cancellationToken);
        Task<bool> DeductCoinsAsync(List<Guid> userIds, int amount, CancellationToken cancellationToken);
        Task UpdateAsync(User user, CancellationToken cancellationToken);
        Task<List<User>> GetRandomBotsAsync(int count, List<Guid> excludeUserIds, CancellationToken cancellationToken);
    }
}
