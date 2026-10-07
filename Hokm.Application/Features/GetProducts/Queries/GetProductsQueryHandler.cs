using ErrorOr;
using Hokm.Application.DTOs.Product;
using Hokm.Application.Interfaces;
using MediatR;
using System.Linq;

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
            var products = await _productRepository.GetActiveProductsAsync(request.FilterType,request.FilterPaymentType, cancellationToken);

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
                p.IsFree,
                p.Items?
                    .Where(i => i.IsActive)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new ProductItemDto(
                        i.Id,
                        i.Content,
                        i.LinkedProductId,
                        i.Quantity,
                        i.SortOrder
                    )).ToList() ?? new List<ProductItemDto>(),
                p.MarketSku
            )).ToList();

            return productDtos;
        }
    }
}