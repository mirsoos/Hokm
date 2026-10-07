namespace Hokm.Application.DTOs.Product
{
    public record ProductItemDto(
        Guid Id,
        string? Content,
        Guid? LinkedProductId,
        int Quantity,
        int SortOrder
    );
}
