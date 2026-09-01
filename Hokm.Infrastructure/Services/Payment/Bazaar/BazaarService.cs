using Hokm.Application.Interfaces;
using Hokm.Domain.Enums;

namespace Hokm.Infrastructure.Services.Payment.Bazaar
{
    public class BazaarService : IStorePaymentService
    {
        public async Task<bool> VerifyStorePurchaseAsync(
            string marketProductId,
            string purchaseToken,
            GatewayType gateway)
        {
            await Task.Delay(100);

            return true;
        }
    }
}
