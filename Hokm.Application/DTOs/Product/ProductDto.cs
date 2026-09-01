using Hokm.Domain.Enums;

namespace Hokm.Application.DTOs.Product
{
    public record ProductDto(
        Guid Id,
        string Title,
        string? Description,
        string AssetKey,
        ProductType ProductType,
        PaymentType PaymentType,
        long Price,
        int? CoinAmount,
        int? VipDurationDays,
        bool IsFree
    );
}
