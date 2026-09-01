using Hokm.Domain.Enums;

namespace Hokm.Application.Interfaces
{
    public interface IStorePaymentService
    {
        Task<bool> VerifyStorePurchaseAsync(
            string marketProductId,
            string purchaseToken,
            GatewayType gateway);
    }
}
