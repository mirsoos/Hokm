using Hokm.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hokm.Infrastructure.Services.Payment.Zarinpal
{
    public class ZarinpalService : IDirectPaymentService
    {
        public async Task<string> RequestPaymentUrlAsync(
            string invoiceNumber,
            decimal amount,
            string productTitle,
            string callbackUrl)
        {
            await Task.Delay(100);

            string mockAuthority = $"zarinpal-auth-{invoiceNumber}";
            return $"https://sandbox.zarinpal.com/pg/StartPay/{mockAuthority}";
        }

        public async Task<bool> VerifyPaymentAsync(string authority, decimal amount)
        {
            await Task.Delay(100);

            return true;
        }
    }
}
