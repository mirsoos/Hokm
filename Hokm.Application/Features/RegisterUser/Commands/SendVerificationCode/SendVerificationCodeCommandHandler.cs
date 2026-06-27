using ErrorOr;
using Hokm.Application.DTOs.Auth;
using Hokm.Application.Interfaces;
using Hokm.Infrastructure.Services.Redis.Constants;
using Hokm.Infrastructure.Services.Redis.Interfaces;
using MediatR;

namespace Hokm.Application.Features.RegisterUser.Commands.SendVerificationCode
{
    public class SendVerificationCodeCommandHandler : IRequestHandler<SendVerificationCodeCommand, ErrorOr<SendCodeResponse>>
    {
        private readonly ISmsService _smsService;
        private readonly IRedisCacheService _cacheService;

        public SendVerificationCodeCommandHandler(ISmsService smsService, IRedisCacheService cacheService)
        {
            _smsService = smsService;
            _cacheService = cacheService;
        }

        public async Task<ErrorOr<SendCodeResponse>> Handle(SendVerificationCodeCommand request, CancellationToken cancellationToken)
        {
            var code = Random.Shared.Next(100000, 999999).ToString();
            var key = RedisCacheKeySchema.VerificationCodeKey(request.PhoneNumber);

            try
            {
                await _cacheService.SetAsync(key, code, TimeSpan.FromMinutes(2), cancellationToken);
            }
            catch (Exception)
            {
                return Error.Unexpected(code: "Redis.Error", description: "خطا در برقراری ارتباط با سرور");
            }

            var smsResult = await _smsService.SendVerificationCodeAsync(request.PhoneNumber, code);

            if (!smsResult.IsSuccess)
            {
                try
                {
                    await _cacheService.RemoveAsync(key, CancellationToken.None);
                }
                catch
                {
                    // log error
                }
                return Error.Failure(code: "Sms.Failure", description: smsResult.Message);
            }
            return new SendCodeResponse
            {
                Message = smsResult.Message
            };
        }
    }
}