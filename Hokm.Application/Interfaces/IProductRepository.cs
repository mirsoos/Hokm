using Hokm.Domain.Entities;
using Hokm.Domain.Enums;

namespace Hokm.Application.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task<List<Product>> GetActiveProductsAsync(ProductType? type, CancellationToken cancellationToken);
    }
}
