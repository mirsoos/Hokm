using Hokm.Domain.Entities;
using Hokm.Domain.Enums;

namespace Hokm.Application.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task<List<Product>> GetActiveProductsAsync(ProductType? type,PaymentType? paymentType, CancellationToken cancellationToken);
        Task<Product?> GetByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default);
        Task<List<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    }
}
