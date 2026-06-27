using ErrorOr;
using Hokm.Application.DTOs.Auth;
using Hokm.Application.Interfaces;
using Hokm.Infrastructure.Services.Redis.Constants;
using Hokm.Infrastructure.Services.Redis.Interfaces;
using MediatR;

namespace Hokm.Application.Features.RegisterUser.Commands.VerifyCode
{
    public class VerifyCodeCommandHandler : IRequestHandler<VerifyCodeCommand, ErrorOr<VerifyCodeResponse>>
    {
        private readonly IRedisCacheService _cacheService;
        private readonly IUserRepository _userRepository;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;

        public VerifyCodeCommandHandler(IRedisCacheService cacheService, IUserRepository userRepository, IJwtTokenGenerator jwtTokenGenerator)
        {
            _cacheService = cacheService;
            _userRepository = userRepository;
            _jwtTokenGenerator = jwtTokenGenerator;
        }

        public async Task<ErrorOr<VerifyCodeResponse>> Handle(VerifyCodeCommand request, CancellationToken cancellationToken)
        {
            var key = RedisCacheKeySchema.VerificationCodeKey(request.PhoneNumber);
            var storedCode = await _cacheService.GetAsync<string>(key, cancellationToken);

            if (string.IsNullOrEmpty(storedCode))
                return Error.NotFound(code: "Auth.CodeExpired", description: "کد تایید منقضی شده یا وجود ندارد.");

            if (storedCode != request.Code)
                return Error.Validation(code: "Auth.InvalidCode", description: "کد وارد شده اشتباه است.");

            await _cacheService.RemoveAsync(key, CancellationToken.None);

            var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber, cancellationToken);

            if (user != null)
            {
                var token = _jwtTokenGenerator.GenerateToken(user.Id, user.PhoneNumber, user.UserName);
                return new VerifyCodeResponse
                {
                    Message = "ورود با موفقیت انجام شد.",
                    IsNewUser = false,
                    Token = token
                };
            }
            else
            {
                var verifiedKey = $"verified-phone:{request.PhoneNumber}";
                await _cacheService.SetAsync(verifiedKey, "true", TimeSpan.FromMinutes(15), cancellationToken);

                return new VerifyCodeResponse
                {
                    Message = "شماره موبایل تایید شد. لطفا پروفایل خود را تکمیل کنید.",
                    IsNewUser = true,
                    Token = null
                };
            }
        }
    }
}