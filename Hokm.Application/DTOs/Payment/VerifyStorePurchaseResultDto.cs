
namespace Hokm.Application.DTOs.Payment
{
    public record VerifyStorePurchaseResultDto(
        bool Success,
        string Message
    );
}
