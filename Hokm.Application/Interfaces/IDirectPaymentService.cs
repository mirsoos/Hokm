
namespace Hokm.Application.Interfaces
{
    public interface IDirectPaymentService
    {
        Task<string> RequestPaymentUrlAsync(
            string invoiceNumber,
            decimal amount,
            string productTitle,
            string callbackUrl);

        Task<bool> VerifyPaymentAsync(string authority, decimal amount);
    }
}
