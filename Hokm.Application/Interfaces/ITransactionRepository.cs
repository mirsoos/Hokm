using Hokm.Domain.Entities;

namespace Hokm.Application.Interfaces
{
    public interface ITransactionRepository
    {
        Task CreateAsync(Transaction transaction, CancellationToken cancellationToken = default);
        Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    }
}
