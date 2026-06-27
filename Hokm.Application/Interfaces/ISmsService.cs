using Hokm.Application.DTOs.Sms;

namespace Hokm.Application.Interfaces
{
    public interface ISmsService
    {
        Task<SmsSendResult> SendVerificationCodeAsync(string phoneNumber , string code);
    }
}
