using ErrorOr;
using Hokm.Application.DTOs.Product;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.GetProducts.Queries
{
    public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, ErrorOr<List<ProductDto>>>
    {
        private readonly IProductRepository _productRepository;

        public GetProductsQueryHandler(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<ErrorOr<List<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
        {
            var products = await _productRepository.GetActiveProductsAsync(request.FilterType, cancellationToken);

            var productDtos = products.Select(p => new ProductDto(
                p.Id,
                p.Title,
                p.Description,
                p.AssetKey,
                p.ProductType,
                p.PaymentType,
                p.Price,
                p.CoinAmount,
                p.VipDurationDays,
                p.IsFree
            )).ToList();

            return productDtos;
        }
    }
}
