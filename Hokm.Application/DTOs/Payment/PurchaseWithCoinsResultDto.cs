namespace Hokm.Application.DTOs.Payment
{
    public record PurchaseWithCoinsResultDto(
         bool Success,
         string Message,
         long RemainingCoins
     );
}
