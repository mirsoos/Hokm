using ErrorOr;
using Hokm.Application.DTOs.Product;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.GetProducts.Queries
{
    public record GetProductsQuery(ProductType? FilterType) : IRequest<ErrorOr<List<ProductDto>>>;

}
